using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octokit;
using Octokit.Internal;

namespace BlogAzureFunctions
{
    public static class PostCommentToPullRequestFunction
    {
        private static readonly Regex emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        private static readonly Regex pathValidChars = new Regex(@"");

        [FunctionName("PostComment")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage request, TraceWriter log)
        {
            if (!request.Content.IsFormData())
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "Form data is missing.");

            var form = await request.Content.ReadAsFormDataAsync();
            if (!TryValidateForm(form, out var errors))
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, String.Join("\n", errors));

            await CreateCommentAsPullRequest(form);

            return request.CreateResponse(HttpStatusCode.OK);
        }

        private static bool TryValidateForm(NameValueCollection form, out List<string> errors)
        {
            errors = new List<string>();
            var missingParameters = new[] { "post_id", "comment", "author", "email" }
                .Select(k => form[k]).Where(String.IsNullOrWhiteSpace).ToArray();

            if (missingParameters.Length > 0)
                errors.Add($"Form values missing for {String.Join(", ", missingParameters)}");

            if (!emailRegex.IsMatch(form["email"]))
                errors.Add("Form 'email' is not the correct format");

            if (!Uri.TryCreate(form["url"], UriKind.Absolute, out var parsedUrl))
                errors.Add("Form 'url' is not the correct format");
            else
                form["url"] = parsedUrl.ToString();

            return errors.Count == 0;
        }

        private static async Task<PullRequest> CreateCommentAsPullRequest(NameValueCollection form)
        {
            var gh = new GitHubClient(
                new ProductHeaderValue("PostCommentToPullRequest"),
                new InMemoryCredentialStore(new Credentials(ConfigurationManager.AppSettings["GitHubToken"])));

            var repoId = ConfigurationManager.AppSettings["PullRequestRepository"].Split('/');
            var repo = await gh.Repository.Get(repoId[0], repoId[1]);
            var defaultBranch = await gh.Repository.Branch.Get(repo.Id, repo.DefaultBranch);

            var commentId = CreateId(form);
            var postId = pathValidChars.Replace(form["post_id"], "-");
            var message = form["comment"];

            var newReference = new NewReference($"refs/heads/comment-{commentId}", defaultBranch.Commit.Sha);
            var newBranch = await gh.Git.Reference.Create(repo.Id, newReference);

            var committer = new Committer(form["author"], form["email"], DateTime.UtcNow);
            var fileContents = CreateFileContents(commentId, committer.Name, committer.Email, form["url"], message);
            var fileRequest = new CreateFileRequest($"Comment by {form["author"]} on {postId}", fileContents, newBranch.Ref);
            fileRequest.Committer = committer;
            await gh.Repository.Content.CreateFile(repo.Id, $"_data/comments/{postId}/{commentId}.yml", fileRequest);

            var pullRequest = new NewPullRequest(fileRequest.Message, newBranch.Ref, defaultBranch.Name) { Body = message };
            return await gh.Repository.PullRequest.Create(repo.Id, pullRequest);
        }

        private static string EncodeGravatar(string email)
        {
            using (var md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(email))).Replace("-", "").ToLower();
        }

        private static string CreateFileContents(int commentId, string author, string email, string url, string message)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"id: {commentId}");
            builder.AppendLine($"name: {author}");
            builder.AppendLine($"email: {email}");
            builder.AppendLine($"gravatar: {EncodeGravatar(email)}");
            if (url != null)
                builder.AppendLine($"url: {url}");
            builder.AppendLine($"date: {DateTime.Now:u}");
            builder.AppendLine($"message: \"{message.Replace("\"", "\\\"")}, \"");
            return builder.ToString();
        }

        private static int CreateId(NameValueCollection form)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * form["post_id"].GetHashCode();
                hash = hash * form["author"].GetHashCode();
                hash = hash * form["comment"].GetHashCode();
                hash = hash * DateTime.Now.GetHashCode();
                return hash;
            }
        }
    }
}
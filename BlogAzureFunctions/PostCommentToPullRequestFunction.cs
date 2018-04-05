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

            var comment = Comment.BuildFromForm(form);

            var newReference = new NewReference($"refs/heads/comment-{comment.id}", defaultBranch.Commit.Sha);
            var newBranch = await gh.Git.Reference.Create(repo.Id, newReference);

            var committer = new Committer(form["author"], form["email"], DateTime.UtcNow);
            var fileContents = CreateFileContents(comment);
            var fileRequest = new CreateFileRequest($"Comment by {form["author"]} on {comment.post_id}", fileContents, newBranch.Ref);
            fileRequest.Committer = committer;
            await gh.Repository.Content.CreateFile(repo.Id, $"_data/comments/{comment.post_id}/{comment.id}.yml", fileRequest);

            var pullRequest = new NewPullRequest(fileRequest.Message, newBranch.Ref, defaultBranch.Name) { Body = comment.message };
            return await gh.Repository.PullRequest.Create(repo.Id, pullRequest);
        }

        private static string CreateFileContents(Comment comment)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"id: {comment.id}");
            builder.AppendLine($"name: {comment.author}");
            builder.AppendLine($"email: {comment.email}");
            builder.AppendLine($"gravatar: {comment.gravatar}");
            builder.AppendLine($"url: {comment.url}");
            builder.AppendLine($"date: {comment.date:u}");
            builder.AppendLine($"message: {comment.message}");
            return builder.ToString();
        }

        class Comment
        {
            private static readonly Regex pathValidChars = new Regex(@"[^a-zA-Z-]");

            public static Comment BuildFromForm(NameValueCollection form)
            {
                return new Comment(
                    pathValidChars.Replace(form["post_id"], "-"),
                    form["comment"],
                    form["author"],
                    form["email"],
                    DateTime.UtcNow,
                    Uri.TryCreate(form["url"], UriKind.Absolute, out var parsedUrl) ? parsedUrl : null);
            }

            public Comment(string post_id, string message, string author, string email, DateTime date,
                Uri url = null, int? id = null, string gravatar = null)
            {
                this.post_id = post_id;
                this.message = message;
                this.author = author;
                this.email = email;
                this.date = date;
                this.url = url;

                this.id = id ?? new { post_id, author, message, date }.GetHashCode();
                this.gravatar = gravatar ?? EncodeGravatar(email);
            }

            public int id { get; }
            public string post_id { get; }
            public string message { get; }
            public string author { get; }
            public string email { get; }
            public Uri url { get; }
            public string gravatar { get; }
            public DateTime date { get; }

            private static string EncodeGravatar(string email)
            {
                using (var md5 = MD5.Create())
                    return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(email))).Replace("-", "").ToLower();
            }
        }
    }
}
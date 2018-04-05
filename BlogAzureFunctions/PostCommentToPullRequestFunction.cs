using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Octokit;
using Octokit.Internal;

namespace BlogAzureFunctions
{
    public static class PostCommentToPullRequestFunction
    {
        private static readonly string[] requiredParams = { "post_id", "comment", "author", "email" };

        [FunctionName("PostComment")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage req, TraceWriter log)
        {
            if (!req.Content.IsFormData())
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Form data is missing.");

            var form = await req.Content.ReadAsFormDataAsync();

            var validationError = ValidateRequiredParams(form);
            if (validationError != null)
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, validationError);

            await PostCommentAsPullRequest(form);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private static string ValidateRequiredParams(NameValueCollection form)
        {
            {
                var missingParameters = requiredParams.Except(form.AllKeys).ToArray();
                if (missingParameters.Length > 0)
                    return $"Form keys are missing for {String.Join(", ", missingParameters)}";
            }

            {
                var missingData = form.AllKeys.Where(k => requiredParams.Contains(k) && String.IsNullOrWhiteSpace(form[k])).ToArray();
                if (missingData.Length > 0)
                    return $"Form values are missing for {String.Join(", ", missingData)}";
            }

            return null;
        }

        private static int CreateId(NameValueCollection form)
        {
            unchecked
            {
                var hash = 17;
                foreach (var key in form.AllKeys)
                    hash = hash * form[key].GetHashCode();
                return hash;
            }
        }

        private static async Task<PullRequest> PostCommentAsPullRequest(NameValueCollection form)
        {
            var repoNameOrg = ConfigurationManager.AppSettings["PullRequestRepository"].Split('/');
            var githubClient = GetGitHubClient();
            var repository = await githubClient.Repository.Get(repoNameOrg[0], repoNameOrg[1]);
            var defaultBranch = await githubClient.Repository.Branch.Get(repository.Id, repository.DefaultBranch);

            var commentId = CreateId(form);
            var postId = form["post_id"];
            var author = form["author"];
            var email = form["email"];
            var message = form["message"];
            var filePath = $"_data/comments/{postId}/{commentId}.yml";
            var commitMessage = $"Comment by {author} on {postId}";

            var newBranch = await CreateBranch($"comment-{commentId}", githubClient, repository, defaultBranch);
            var fileContents = GetFileContents(commentId, author, email, form["url"], message);
            var fileRequest = new CreateFileRequest(commitMessage, fileContents, newBranch.Ref)
            {
                Committer = new Committer(author, email, DateTime.UtcNow)
            };
            await githubClient.Repository.Content.CreateFile(repository.Id, filePath, fileRequest);

            return await CreatePullRequest(githubClient, repository.Id, newBranch, defaultBranch, commitMessage, message);
        }

        private static Task<PullRequest> CreatePullRequest(IGitHubClient githubClient, long repositoryId, Reference newBranch, Branch defaultBranch, string commitMessage, string body)
        {
            var pullRequest = new NewPullRequest(commitMessage, newBranch.Ref, defaultBranch.Name) { Body = body };
            return githubClient.Repository.PullRequest.Create(repositoryId, pullRequest);
        }

        private static Task<Reference> CreateBranch(string branchName, IGitHubClient githubClient, Repository repository, Branch defaultBranch)
        {
            // TODO: Encode or validate branch name
            return githubClient.Git.Reference.Create(repository.Id, new NewReference($"refs/heads/{branchName}", defaultBranch.Commit.Sha));
        }

        private static string CreateGravatar(string email)
        {
            using (var md5 = MD5.Create())
                return BytesToHex(md5.ComputeHash(Encoding.UTF8.GetBytes(email)));
        }

        private static GitHubClient GetGitHubClient()
        {
            return new GitHubClient(
                new ProductHeaderValue("PostCommentToPullRequest"),
                new InMemoryCredentialStore(new Credentials(ConfigurationManager.AppSettings["GitHubToken"])));
        }

        private static string BytesToHex(byte[] bytes)
        {
            var hex = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var pair = bytes[i].ToString("x2");
                hex[i * 2] = pair[0];
                hex[i * 2 + 1] = pair[1];
            }

            return new String(hex);
        }

        private static string GetFileContents(int commentId, string author, string email, string url, string message)
        {
            // TODO: Replace with a YAML builder capable of correct encoding
            var builder = new StringBuilder();
            builder.AppendLine($"id: {commentId}");
            builder.AppendLine($"name: {author}");
            builder.AppendLine($"email: {email}");
            builder.AppendLine($"gravatar: {CreateGravatar(email)}");
            if (url != null)
                builder.AppendLine($"url: {url}");
            builder.AppendLine($"date: {DateTime.Now:u}");
            builder.AppendLine($"message: \"{message.Replace("\"", "\\\"")}, \"");
            return builder.ToString();
        }
    }
}
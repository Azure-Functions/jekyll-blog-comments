using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
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
        private static readonly string[] optionalParams = { "url" };

        [FunctionName("PostComment")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage req, TraceWriter log)
        {
            if (!req.Content.IsFormData())
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Form data is missing.");

            var form = await req.GetFormAsDictionary();

            var validationError = ValidateRequiredParams(form);
            if (validationError != null)
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, validationError);

            await PostCommentAsPullRequest(new NewComment(form["post_id"], form["comment"], form["author"], form["email"], form["url"]));

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private static string ValidateRequiredParams(Dictionary<string, string> form)
        {
            {
                var missingParameters = requiredParams.Except(form.Keys).ToArray();
                if (missingParameters.Length > 0)
                    return $"Form keys are missing for {String.Join(", ", missingParameters)}";
            }

            {
                var missingData = form.Where(kv => requiredParams.Contains(kv.Key) && String.IsNullOrWhiteSpace(kv.Value)).Select(k => k.Key).ToArray();
                if (missingData.Length > 0)
                    return $"Form values are missing for {String.Join(", ", missingData)}";
            }

            return null;
        }

        private static async Task<PullRequest> PostCommentAsPullRequest(NewComment comment)
        {
            var repositoryId = long.Parse(ConfigurationManager.AppSettings["PullRequestRepositoryId"]);
            var fallbackCommiterEmail = ConfigurationManager.AppSettings["PullRequestFallbackCommiterId"];
            var githubClient = GetGitHubClient();
            return await PostCommentAsPullRequest(comment, githubClient, repositoryId, fallbackCommiterEmail);
        }

        private static async Task<PullRequest> PostCommentAsPullRequest(NewComment comment, IGitHubClient githubClient, long repositoryId, string fallbackCommiterEmail)
        {
            var repository = await githubClient.Repository.Get(repositoryId);
            var defaultBranch = await githubClient.Repository.Branch.Get(repository.Id, repository.DefaultBranch);

            var newBranch = await CreateBranch($"comment-{comment.Id}", githubClient, repository, defaultBranch);

            var fileRequest = CreateFileRequest(comment, fallbackCommiterEmail, newBranch);
            await githubClient.Repository.Content.CreateFile(repository.Id, comment.GetFilePath(), fileRequest);

            return await CreatePullRequest(githubClient, repositoryId, newBranch, defaultBranch, comment.GetCommitMessage(), comment.Message);
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

        private static CreateFileRequest CreateFileRequest(NewComment comment, string fallbackCommiterEmail, Reference newBranch)
        {
            return new CreateFileRequest(comment.GetCommitMessage(), GetFileContents(comment), newBranch.Ref)
            {
                Committer = new Committer(comment.Author, comment.Email ?? fallbackCommiterEmail, comment.When)
            };
        }

        private static string CreateGravatar(string email)
        {
            using (var md5 = MD5.Create())
                return md5.ComputeHash(Encoding.UTF8.GetBytes(email)).ToHex();
        }

        private static InMemoryCredentialStore GetGitHubInMemoryCredentialsStore()
        {
            var token = ConfigurationManager.AppSettings["GitHubToken"] ?? "69ecb5590653aa354302ba069b8a2a3b5a6c76ee";
            return new InMemoryCredentialStore(new Credentials(token));
        }

        private static GitHubClient GetGitHubClient()
        {
            var product = new ProductHeaderValue("PostCommentToPullRequest");
            return new GitHubClient(product, GetGitHubInMemoryCredentialsStore());
        }

        private static string ToHex(this byte[] bytes)
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

        private static string GetFileContents(NewComment comment)
        {
            // TODO: Replace with a YAML builder capable of correct encoding
            var builder = new StringBuilder();
            builder.AppendLine($"id: {comment.Id} ");
            builder.AppendLine($"name: {comment.Author}");

            if (!String.IsNullOrWhiteSpace(comment.Email))
            {
                builder.AppendLine($"email: {comment.Email}");
                builder.AppendLine($"gravatar: {CreateGravatar(comment.Email)}");
            }

            if (comment.Url != null)
            {
                builder.AppendLine($"url: {comment.Url}");
            }

            builder.AppendLine($"date: {DateTime.Now:u}");
            builder.AppendLine($"message: \"{comment.Message.Replace("\"", "\\\"")}, \"");
            return builder.ToString();
        }

        private static string GetCommitMessage(this NewComment comment)
        {
            return $"Comment by {comment.Author} on {comment.PostId}";
        }

        private static string GetFilePath(this NewComment comment)
        {
            return $"_data/comments/{comment.PostId}/{comment.Id}.yml";
        }
    }
}
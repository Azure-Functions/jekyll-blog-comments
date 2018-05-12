using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Octokit;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace BlogAzureFunctions
{
    public static class PostCommentToPullRequestFunction
    {
        struct MissingRequiredValue { }
        static readonly Regex pathValidChars = new Regex(@"[^a-zA-Z-]");
        static readonly Regex emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        [FunctionName("PostComment")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage request)
        {
            var form = await request.Content.ReadAsFormDataAsync();
            if (TryCreateCommentFromForm(form, out var comment, out var errors))
                await CreateCommentAsPullRequest(comment);

            var response = request.CreateResponse(errors.Any() ? HttpStatusCode.BadRequest : HttpStatusCode.OK, String.Join("\n", errors));
            if (form["redirect"] != null)
                response.Headers.Location = new Uri(form["redirect"]);
            return response;
        }

        private static async Task<PullRequest> CreateCommentAsPullRequest(Comment comment)
        {
            var github = new GitHubClient(new ProductHeaderValue("PostCommentToPullRequest"),
                new Octokit.Internal.InMemoryCredentialStore(new Credentials(ConfigurationManager.AppSettings["GitHubToken"])));

            var repoOwnerName = ConfigurationManager.AppSettings["PullRequestRepository"].Split('/');
            var repo = await github.Repository.Get(repoOwnerName[0], repoOwnerName[1]);

            var defaultBranch = await github.Repository.Branch.Get(repo.Id, repo.DefaultBranch);
            var newBranch = await github.Git.Reference.Create(repo.Id, new NewReference($"refs/heads/comment-{comment.id}", defaultBranch.Commit.Sha));
            var fileRequest = new CreateFileRequest($"Comment by {comment.name} on {comment.post_id}", new SerializerBuilder().Build().Serialize(comment), newBranch.Ref);
            fileRequest.Committer = new Committer(comment.name, comment.email, comment.date);
            await github.Repository.Content.CreateFile(repo.Id, $"_data/comments/{comment.post_id}/{comment.id}.yml", fileRequest);

            return await github.Repository.PullRequest.Create(repo.Id, new NewPullRequest(fileRequest.Message, newBranch.Ref, defaultBranch.Name) { Body = comment.message });
        }

        private static object ConvertParameter(string parameter, Type targetType)
        {
            return String.IsNullOrWhiteSpace(parameter) ? null : System.ComponentModel.TypeDescriptor.GetConverter(targetType).ConvertFrom(parameter);
        }

        private static bool TryCreateCommentFromForm(System.Collections.Specialized.NameValueCollection form, out Comment comment, out List<string> errors)
        {
            var constructor = typeof(Comment).GetConstructors()[0];
            var values = constructor.GetParameters()
                .ToDictionary(p => p.Name, p => ConvertParameter(form[p.Name], p.ParameterType) ?? (p.HasDefaultValue ? p.DefaultValue : new MissingRequiredValue()));

            errors = values.Where(p => p.Value is MissingRequiredValue).Select(p => $"Form value missing for {p.Key}").ToList();
            if (values["email"] is string s && !emailRegex.IsMatch(s))
                errors.Add("email not in correct format");

            comment = errors.Any() ? null : (Comment)constructor.Invoke(values.Values.ToArray());
            return !errors.Any();
        }

        private class Comment
        {
            public Comment(string post_id, string message, string name, string email, DateTime? date = null, Uri url = null, int? id = null, string gravatar = null)
            {
                this.post_id = pathValidChars.Replace(post_id, "-");
                this.message = message;
                this.name = name;
                this.email = email;
                this.date = date ?? DateTime.UtcNow;
                this.url = url;
                this.id = id ?? new { this.post_id, this.name, this.message, this.date }.GetHashCode();
                this.gravatar = gravatar ?? EncodeGravatar(email);
            }

            [YamlIgnore]
            public string post_id { get; }

            public int id { get; }
            public DateTime date { get; }
            public string name { get; }
            public string email { get; }
            public string gravatar { get; }

            [YamlMember(typeof(string))]
            public Uri url { get; }

            public string message { get; }

            static string EncodeGravatar(string email)
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                    return BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(email))).Replace("-", "").ToLower();
            }
        }
    }
}

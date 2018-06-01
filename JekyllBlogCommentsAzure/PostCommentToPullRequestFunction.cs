using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace JekyllBlogCommentsAzure
{
    public static class PostCommentToPullRequestFunction
    {
        struct MissingRequiredValue { } // Placeholder for missing required form values
        static readonly Regex validPathChars = new Regex(@"[^a-zA-Z0-9-]"); // Valid characters when mapping from the blog post slug to a file path
        static readonly Regex validEmail = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$"); // Simplest form of email validation

        [FunctionName("PostComment")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage request)
        {
            var form = await request.Content.ReadAsFormDataAsync();

            // Make sure the site posting the comment is the correct site.
            var commentSite = ConfigurationManager.AppSettings["CommentWebsiteUrl"];
            if (!String.IsNullOrWhiteSpace(commentSite) && !AreSameSites(commentSite, form["comment-site"]))
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "Please make sure you post this to your own Jekyll comments receiever.");

            if (TryCreateCommentFromForm(form, out var comment, out var errors))
                await CreateCommentAsPullRequest(comment);

            if (errors.Any())
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, String.Join("\n", errors));

            if (!Uri.TryCreate(form["redirect"], UriKind.Absolute, out var redirectUri))
                return request.CreateResponse(HttpStatusCode.OK);

            var response = request.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Location = redirectUri;
            return response;
        }

        private static bool AreSameSites(string commentSite, string postedCommentSite)
        {
            return Uri.TryCreate(commentSite, UriKind.Absolute, out var commentSiteUri)
                && Uri.TryCreate(postedCommentSite, UriKind.Absolute, out var postedCommentSiteUri)
                && commentSiteUri.Host.Equals(postedCommentSiteUri.Host, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<PullRequest> CreateCommentAsPullRequest(Comment comment)
        {
            // Create the Octokit client
            var github = new GitHubClient(new ProductHeaderValue("PostCommentToPullRequest"),
                new Octokit.Internal.InMemoryCredentialStore(new Credentials(ConfigurationManager.AppSettings["GitHubToken"])));

            // Get a reference to our GitHub repository
            var repoOwnerName = ConfigurationManager.AppSettings["PullRequestRepository"].Split('/');
            var repo = await github.Repository.Get(repoOwnerName[0], repoOwnerName[1]);

            // Create a new branch from the default branch
            var defaultBranch = await github.Repository.Branch.Get(repo.Id, repo.DefaultBranch);
            var newBranch = await github.Git.Reference.Create(repo.Id, new NewReference($"refs/heads/comment-{comment.id}", defaultBranch.Commit.Sha));

            // Create a new file with the comments in it
            var fileRequest = new CreateFileRequest($"Comment by {comment.name} on {comment.post_id}", new SerializerBuilder().Build().Serialize(comment), newBranch.Ref)
            {
                Committer = new Committer(comment.name, comment.email, comment.date)
            };
            await github.Repository.Content.CreateFile(repo.Id, $"_data/comments/{comment.post_id}/{comment.id}.yml", fileRequest);

            // Create a pull request for the new branch and file
            return await github.Repository.PullRequest.Create(repo.Id, new NewPullRequest(fileRequest.Message, newBranch.Ref, defaultBranch.Name)
            {
                Body = comment.message
            });
        }

        private static object ConvertParameter(string parameter, Type targetType)
        {
            return String.IsNullOrWhiteSpace(parameter)
                ? null
                : TypeDescriptor.GetConverter(targetType).ConvertFrom(parameter);
        }

        /// <summary>
        /// Try to create a Comment from the form.  Each Comment constructor argument will be name-matched
        /// against values in the form. Each non-optional arguments (those that don't have a default value)
        /// not supplied will cause an error in the list of errors and prevent the Comment from being created.
        /// </summary>
        /// <param name="form">Incoming form submission as a <see cref="NameValueCollection"/>.</param>
        /// <param name="comment">Created <see cref="Comment"/> if no errors occurred.</param>
        /// <param name="errors">A list containing any potential validation errors.</param>
        /// <returns>True if the Comment was able to be created, false if validation errors occurred.</returns>
        private static bool TryCreateCommentFromForm(NameValueCollection form, out Comment comment, out List<string> errors)
        {
            var constructor = typeof(Comment).GetConstructors()[0];
            var values = constructor.GetParameters()
                .ToDictionary(
                    p => p.Name,
                    p => ConvertParameter(form[p.Name], p.ParameterType) ?? (p.HasDefaultValue ? p.DefaultValue : new MissingRequiredValue())
                );

            errors = values.Where(p => p.Value is MissingRequiredValue).Select(p => $"Form value missing for {p.Key}").ToList();
            if (values["email"] is string s && !validEmail.IsMatch(s))
                errors.Add("email not in correct format");

            comment = errors.Any() ? null : (Comment)constructor.Invoke(values.Values.ToArray());
            return !errors.Any();
        }

        /// <summary>
        /// Represents a Comment to be written to the repository in YML format.
        /// </summary>
        private class Comment
        {
            public Comment(string post_id, string message, string name, string email, DateTime? date = null, Uri url = null, int? id = null, string gravatar = null)
            {
                this.post_id = validPathChars.Replace(post_id, "-");
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

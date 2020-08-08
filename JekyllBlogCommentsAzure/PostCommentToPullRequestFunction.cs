using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Octokit;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace JekyllBlogCommentsAzure
{
    public static class PostCommentToPullRequestFunction
    {
        public static readonly WebConfigurator config = new WebConfigurator();

        [FunctionName("PostComment")] // Actual form post handler
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage request)
        {
            var form = await request.Content.ReadAsFormDataAsync();

            // Make sure the site posting the comment is the correct site.
            var allowedSite = config.CommentWebsiteUrl;
            var postedSite = form["comment-site"];
            if (!String.IsNullOrWhiteSpace(allowedSite) && !AreSameSites(allowedSite, postedSite))
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, $"This Jekyll comments receiever does not handle forms for '${postedSite}'. You should point to your own instance.");

            if (Comment.TryCreateFromForm(form, out var comment, out var errors))
                await CreatePullRequest(comment);

            if (errors.Any())
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, String.Join("\n", errors));

            if (!Uri.TryCreate(form["redirect"], UriKind.Absolute, out var redirectUri))
                return request.CreateResponse(HttpStatusCode.OK);

            var response = request.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Location = redirectUri;
            return response;
        }

        [FunctionName("Preload")] // Ping this to preload the function and avoid cold starts.
        public static HttpResponseMessage Preload([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage request)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private static bool AreSameSites(string commentSite, string postedCommentSite)
        {
            return Uri.TryCreate(commentSite, UriKind.Absolute, out var commentSiteUri)
                && Uri.TryCreate(postedCommentSite, UriKind.Absolute, out var postedCommentSiteUri)
                && commentSiteUri.Host.Equals(postedCommentSiteUri.Host, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<PullRequest> CreatePullRequest(Comment comment)
        {
            // Create the Octokit client
            var github = new GitHubClient(new ProductHeaderValue("PostCommentToPullRequest"),
                new Octokit.Internal.InMemoryCredentialStore(new Credentials(config.GitHubToken)));

            // Get a reference to our GitHub repository
            var repoOwnerName = config.PullRequestRepository.Split('/');
            var repo = await github.Repository.Get(repoOwnerName[0], repoOwnerName[1]);

            string codeBranchName = config.CodeBranch;
            string commentsFolderLocation = config.CommentsFolderLocation;

            // Create a new branch from the default branch
            // var defaultBranch = await github.Repository.Branch.Get(repo.Id, repo.DefaultBranch);
            var codeBranch = await github.Repository.Branch.Get(repo.Id, codeBranchName);
            var newBranch = await github.Git.Reference.Create(repo.Id, new NewReference($"refs/heads/comment-{comment.id}", codeBranch.Commit.Sha));

            // Create a new file with the comments in it
            var fileRequest = new CreateFileRequest($"Comment by {comment.name} on {comment.post_id}", comment.ToYaml(), newBranch.Ref)
            {
                Committer = new Committer(comment.name, comment.email ?? config.CommentFallbackCommitEmail ?? "redacted@example.com", comment.date)
            };
            await github.Repository.Content.CreateFile(repo.Id, $"{commentsFolderLocation}/{comment.post_id}/{comment.id}.yml", fileRequest);

            // Create a pull request for the new branch and file
            return await github.Repository.PullRequest.Create(repo.Id, new NewPullRequest(fileRequest.Message, newBranch.Ref, codeBranch.Name)
            {
                Body = $"avatar: <img src=\"{comment.avatar}\" width=\"64\" height=\"64\" />\n\nScore: {comment.score}\n\n{comment.message}"
            });
        }
    }
}

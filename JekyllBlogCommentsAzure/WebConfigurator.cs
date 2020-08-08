using System.Configuration;

namespace JekyllBlogCommentsAzure
{
    public class WebConfigurator
    {
        public string CommentWebsiteUrl => ConfigurationManager.AppSettings["CommentWebsiteUrl"];

        public string GitHubToken => ConfigurationManager.AppSettings["GitHubToken"];

        public string PullRequestRepository => ConfigurationManager.AppSettings["PullRequestRepository"];

        public string CodeBranch => ConfigurationManager.AppSettings["CodeBranch"];

        public string CommentsFolderLocation => ConfigurationManager.AppSettings["CommentsFolderLocation"];

        public string CommentFallbackCommitEmail => ConfigurationManager.AppSettings["CommentFallbackCommitEmail"];

        public string SentimentAnalysisSubscriptionKey => ConfigurationManager.AppSettings["SentimentAnalysis.SubscriptionKey"];

        public string SentimentAnalysisRegion => ConfigurationManager.AppSettings["SentimentAnalysis.Region"];

        public string SentimentAnalysisLang => ConfigurationManager.AppSettings["SentimentAnalysis.Lang"];
    }
}

# Jekyll Blog Comments Azure Function

An Azure Function App that receives comment form posts and creates a pull request against your GitHub repository as part of the [jekyll-blog-comments](https://github.com/damieng/jekyll-blog-comments) system.

The app includes just one function:

* `PostComment` - receives form POST submission and creates a PR to add the comment to your Jekyll site

## Setup

To set this up, you'll need to have an [Azure Portal account](https://portal.azure.com).

1. Fork this repository
2. [Create a **v1** Azure Function](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-first-azure-function)
3. [Create subscription key in Microsoft Azure Recognition](https://docs.microsoft.com/en-us/azure/cognitive-services/cognitive-services-apis-create-account) **optional**
4. [Set up your function to deploy from your fork](https://docs.microsoft.com/en-us/azure/azure-functions/scripts/functions-cli-create-function-app-github-continuous)
5. Set up the following [App Settings for your Azure Function](https://docs.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings)

| Setting | Value
| -------- | -------
| `PullRequestRepository` | `owner/name` of the repository that houses your Jekyll site for pull requests to be created against. For example, `haacked/haacked.com` will post to https://github.com/haacked/haacked.com
| `GitHubToken` | A [GitHub personal access token](https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/) with access to edit your target repository.
| `CommentWebsiteUrl` | The URL to the website that hosts the comments. This is used to make sure the correct site is posting comments to the receiver.
| `CommentFallbackCommitEmail` | The email address to use for GitHub commits and PR's if the form does not supply one.
| `SentimentAnalysis.SubscriptionKey` | Subscription Key for Microsoft Azure Recognition, if you don't want to use, just leave empty.
| `SentimentAnalysis.Region` | Region for your Subscription key (E.g.: westus)
| `SentimentAlaysis.Lang` | Language for comment, [find lang code here](https://docs.microsoft.com/en-us/azure/cognitive-services/text-analytics/language-support)
# Blog Comments Azure Function

An Azure Function that receives comment form posts for https://github.com/damieng/jekyll-blog-comments, a Jekyll-based blog comment system.

This repository includes two functions:

* `PostComment` - receives a comment form POST submission and creates a PR on the target repository to add a comment to the Jekyll site.
* `PageView` - ???

## Setup

To set this up, you'll need to have an [Azure Portal account](https://portal.azure.com).

0. Fork this repository.
1. [Create an Azure function](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-first-azure-function).
2. [Set up your function to deploy from GitHub](https://docs.microsoft.com/en-us/azure/azure-functions/scripts/functions-cli-create-function-app-github-continuous). Point it to your fork of this repository.
3. Set up the following [App Settings for your Azure Function App](https://docs.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings).

| Setting | Value
| -------- | -------
| `PullRequestRepository` | URL to the repository that houses your Jekyll site.
| `BlogCommentTableStorage` | Name of the Blob storage associated with the Azure Function. You would have chosen this when creating the Azure Function.
| `BlogPartitionKey` | The `PageViewCount` function can store page counts for multiple sites. This specifies which site to store it for this instance of the function.
| `GitHubToken` | A [GitHub personal access token](https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/) with access to edit your target repository. 

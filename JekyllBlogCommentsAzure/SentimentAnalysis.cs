using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Rest;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace JekyllBlogCommentsAzure
{
    public class SentimentAnalysis
    {

        class ApiKeyServiceClientCredentials : ServiceClientCredentials
        {
            public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", ConfigurationManager.AppSettings["SentimentAnalysis.SubscriptionKey"]);
                return base.ProcessHttpRequestAsync(request, cancellationToken);
            }
        }

        public string Analyze(string input)
        {
            ITextAnalyticsClient client = new TextAnalyticsClient(new ApiKeyServiceClientCredentials())
            {
                Endpoint = $"https://{ConfigurationManager.AppSettings["SentimentAnalysis.Region"]}.api.cognitive.microsoft.com"
            };
            SentimentBatchResult result = client.SentimentAsync(
                new MultiLanguageBatchInput(
                    new List<MultiLanguageInput>()
                    {
                        new MultiLanguageInput($"{ConfigurationManager.AppSettings["SentimentAlaysis.Lang"]}","0", input)
                    }
                )).Result;
            return $"{result.Documents[0].Score:0.00}";
        }
    }
}

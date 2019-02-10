using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Rest;

namespace JekyllBlogCommentsAzure
{
    public class SentimentAnalysis
    {
        private readonly string _subscriptionKey;
        private readonly string _region;
        private readonly string _lang;

        public SentimentAnalysis(string subscriptionKey, string region, string lang)
        {
            _subscriptionKey = subscriptionKey;
            _region = region;
            _lang = lang;
        }

        class ApiKeyServiceClientCredentials : ServiceClientCredentials
        {
            private readonly string _subscriptionKey;


            public ApiKeyServiceClientCredentials(string subscriptionKey)
            {
                _subscriptionKey = subscriptionKey;
            }
            public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                return base.ProcessHttpRequestAsync(request, cancellationToken);
            }
        }

        public IEnumerable<MultiLanguageInput> SplitFiveHundredChars(string input)
        {
            int id = 0;
            for (var i = 0; i < input.Length; i += 5000)
            {
                yield return new MultiLanguageInput(_lang, id.ToString(), input.Substring(i, Math.Min(5000, input.Length - i)));
                id++;
            }
        }

        public string Analyze(string input)
        {
            ITextAnalyticsClient client = new TextAnalyticsClient(new ApiKeyServiceClientCredentials(_subscriptionKey))
            {
                Endpoint = $"https://{_region}.api.cognitive.microsoft.com"
            };
            SentimentBatchResult result = client.SentimentAsync(
                new MultiLanguageBatchInput(
                    SplitFiveHundredChars(input).ToList()
                )).Result;
            return $"{result.Documents[0].Score:0.00}";
        }
    }
}

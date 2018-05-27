using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlogAzureFunctions
{
    public class PageViewCount : TableEntity
    {
        public PageViewCount(string pageName)
        {
            PartitionKey = ConfigurationManager.AppSettings["BlogPartitionKey"];
            RowKey = pageName;
        }

        public PageViewCount() { }
        public int ViewCount { get; set; }
    }

    public static class PageViewCountFunction
    {
        [FunctionName("PageView")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")]HttpRequestMessage req, TraceWriter log)
        {
            var page = req.GetQueryParameterValue("page");
            if (String.IsNullOrEmpty(page))
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "'page' parameter missing.");

            var table = Helpers.GetTableReference("PageViewCounts");

            var pageView = await table.RetrieveAsync<PageViewCount>("damieng.com", page) ?? new PageViewCount(page) { ViewCount = 0 };
            var operation = pageView.ViewCount == 0
                ? TableOperation.Insert(pageView)
                : TableOperation.Replace(pageView);

            pageView.ViewCount++;
            await table.ExecuteAsync(operation);

            return req.CreateResponse(HttpStatusCode.OK, new { viewCount = pageView.ViewCount });
        }
    }
}
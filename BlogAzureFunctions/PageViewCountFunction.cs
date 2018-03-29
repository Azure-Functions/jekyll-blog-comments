using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlogAzureFunctions
{
    public class PageViewCount : TableEntity
    {
        public PageViewCount(string pageName)
        {
            PartitionKey = "damieng.com";
            RowKey = pageName;
        }

        public PageViewCount() { }
        public int ViewCount { get; set; }
    }

    public static class PageViewCountFunction
    {
        [FunctionName("PageView")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("PageView received a request.");

            var page = req.GetQueryParameterValue("page");
            if (String.IsNullOrEmpty(page))
            {
                log.Error("'page' parameter missing.");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "'page' parameter missing.");
            }

            var table = Helpers.GetTableReference("PageViewCounts");

            var pageView = await table.RetrieveAsync<PageViewCount>("damieng.com", page);
            if (pageView == null)
            {
                pageView = new PageViewCount(page) { ViewCount = 1 };
                log.Info($"PageView initializing count for page '{page}' to 1");
                await table.ExecuteAsync(TableOperation.Insert(pageView));
            }
            else
            {
                pageView.ViewCount++;
                log.Info($"PageView incrementing count for page '{page}' to {pageView.ViewCount}");
                await table.ExecuteAsync(TableOperation.Replace(pageView));
            }

            log.Info($"PageView complete for '{page}'");

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                page,
                viewCount = pageView.ViewCount
            });
        }
    }
}


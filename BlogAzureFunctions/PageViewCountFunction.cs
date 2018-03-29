using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Configuration;
using System.Linq;
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
            log.Info("PageView function received a request.");

            var page = req.GetQueryNameValuePairs().Where(kv => kv.Key == "page").Select(kv => kv.Value).FirstOrDefault();
            if (String.IsNullOrEmpty(page))
            {
                log.Error("'page' parameter missing.");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "'page' parameter missing.");
            }

            var cloudService = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["DamienGTableStorage"]);
            var table = cloudService.CreateCloudTableClient().GetTableReference("PageViewCounts");

            var retrievedResult = await table.ExecuteAsync(TableOperation.Retrieve<PageViewCount>("damieng.com", page));
            var pageView = (PageViewCount)retrievedResult.Result;
            if (pageView == null)
            {
                pageView = new PageViewCount(page) { ViewCount = 1 };
                await table.ExecuteAsync(TableOperation.Insert(pageView));
            }
            else
            {
                pageView.ViewCount++;
                await table.ExecuteAsync(TableOperation.Replace(pageView));
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                page,
                viewCount = pageView.ViewCount
            });
        }
    }
}


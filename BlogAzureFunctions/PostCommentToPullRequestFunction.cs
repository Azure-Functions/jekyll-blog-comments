using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlogAzureFunctions
{
    public static class PostCommentToPullRequestFunction
    {
        private static readonly string[] requiredParams = { "post_id", "comment", "author" };

        [FunctionName("PostComment")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage req, TraceWriter log)
        {
            if (!req.Content.IsFormData())
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Form data is missing.");

            var form = await req.GetFormAsDictionary();
            var validationError = ValidateForm(form);
            if (validationError != null)
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, validationError);

            var pullRequest = PostCommentAsPullRequest(form["post_id"], form["comment"], form["name"], form["email"], form["url"]);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private static string ValidateForm(Dictionary<string, string> form)
        {
            {
                var missingParameters = requiredParams.Except(form.Keys).ToArray();
                if (missingParameters.Length > 0)
                    return $"Form keys are missing for {String.Join(", ", missingParameters)}";
            }

            {
                var missingData = form.Where(kv => requiredParams.Contains(kv.Key) && String.IsNullOrWhiteSpace(kv.Value)).Select(k => k.Key).ToArray();
                if (missingData.Length > 0)
                    return $"Form values are missing for {String.Join(", ", missingData)}";
            }

            return null;
        }

        private static async Task PostCommentAsPullRequest(string postId, string comment, string name, string email = null, string url = null)
        {

        }
    }
}
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlogAzureFunctions
{
    static class Helpers
    {
        public static string GetQueryParameterValue(this HttpRequestMessage message, string name)
        {
            return message.RequestUri.ParseQueryString()[name];
        }

        public static async Task<Dictionary<string, string>> GetFormAsDictionary(this HttpRequestMessage message)
        {
            var formData = await message.Content.ReadAsFormDataAsync();
            return formData.AllKeys.ToDictionary(k => k, k => formData[k]);
        }

        public static CloudStorageAccount GetCloudStorageAccount()
        {
            var connection = ConfigurationManager.AppSettings["DamienGTableStorage"];
            return connection == null ? CloudStorageAccount.DevelopmentStorageAccount : CloudStorageAccount.Parse(connection);
        }

        public static CloudTable GetTableReference(string name)
        {
            return GetCloudStorageAccount().CreateCloudTableClient().GetTableReference(name);
        }

        public static async Task<T> RetrieveAsync<T>(this CloudTable cloudTable, string partitionKey, string rowKey) where T : TableEntity
        {
            var tableResult = await cloudTable.ExecuteAsync(TableOperation.Retrieve<T>(partitionKey, rowKey));
            return (T)tableResult.Result;
        }

        private static readonly Random random = new Random();

        public static UInt64 GetRandomUInt64()
        {
            var bytes = new Byte[sizeof(ulong)];
            random.NextBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}

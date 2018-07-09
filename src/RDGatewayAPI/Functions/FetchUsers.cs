using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using RDGatewayAPI.Data;

namespace RDGatewayAPI.Functions
{
    public static class FetchUsers
    {
        private const string CONTINUATIONTOKEN_QUERYSTRING_KEY = "$skiptoken";

        private static string SerializeContinuationToken(TableContinuationToken continuationToken)
        {
            using(var buffer = new MemoryStream())
            using(var writer = new XmlTextWriter(buffer, Encoding.UTF8))
            {
                continuationToken.WriteXml(writer);

                writer.Flush();

                return Convert.ToBase64String(buffer.ToArray());
            }
        }

        private static TableContinuationToken DeserializeContinuationToken(string token)
        {
            var continuationToken = new TableContinuationToken();

            using (var buffer = new MemoryStream(Convert.FromBase64String(token)))
            using (var reader = new XmlTextReader(buffer))
            {
                continuationToken.ReadXml(reader);
            }

            return continuationToken;
        }

        private static string CreateNextLink(HttpRequestMessage req, string token)
        {
            var qs = HttpUtility.ParseQueryString(req.RequestUri.Query);
            qs.Set(CONTINUATIONTOKEN_QUERYSTRING_KEY, token);

            var uri = new UriBuilder(req.RequestUri.GetLeftPart(UriPartial.Path));
            uri.Query = qs.ToString();

            return uri.ToString();
        }

        [FunctionName("FetchUsers")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "users")]HttpRequestMessage req,
                                                          [Table("users")] CloudTable userTable, TraceWriter log)
        {
            var continuationToken = default(TableContinuationToken);

            if (req.GetQueryNameValuePairs().ToDictionary(kv => kv.Key, kv => Uri.UnescapeDataString(kv.Value)).TryGetValue(CONTINUATIONTOKEN_QUERYSTRING_KEY, out string token))
            {
                try
                {
                    continuationToken = DeserializeContinuationToken(Uri.UnescapeDataString(token));
                }
                catch (Exception exc)
                {
                    log.Error($"Failed to deserialize continuation token {token}", exc);

                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }
            }

            var segment = await userTable.ExecuteQuerySegmentedAsync<UserEntity>(new TableQuery<UserEntity>(), continuationToken).ConfigureAwait(false);

            var result = new PagedEntities<UserEntity>(segment)
            {
                NextLink = (segment.ContinuationToken != null ? CreateNextLink(req, Uri.EscapeDataString(SerializeContinuationToken(segment.ContinuationToken))) : null)
            };

            return req.CreateResponse(HttpStatusCode.OK, result, "application/json");
        }
    }
}

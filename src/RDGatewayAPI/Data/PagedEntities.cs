/* ------------------------------------------------------------------------------------------------
Copyright (c) 2018 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
------------------------------------------------------------------------------------------------ */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace RDGatewayAPI.Data
{
    public sealed class PagedEntities<T>
        where T : ITableEntity
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault();

        private const string HEADER_X_FORWARDED_HOST = "X-Forwarded-Host";
        private const string HEADER_X_FORWARDED_PROTO = "X-Forwarded-Proto";
        private const string CONTINUATIONTOKEN_QUERYSTRING_KEY = "$skiptoken";

        private static string SerializeContinuationToken(TableContinuationToken continuationToken)
        {
            using var buffer = new MemoryStream();
            using var writer = new JsonTextWriter(new StreamWriter(buffer));

            Serializer.Serialize(writer, continuationToken);

            writer.Flush();

            return Convert.ToBase64String(buffer.ToArray());
        }

        private static TableContinuationToken DeserializeContinuationToken(string token)
        {
            using var buffer = new MemoryStream(Convert.FromBase64String(token));
            using var reader = new JsonTextReader(new StreamReader(buffer));

            return Serializer.Deserialize<TableContinuationToken>(reader);
        }

        public static string GetNextLink(HttpRequest request, TableContinuationToken continuationToken)
        {
            var qs = HttpUtility.ParseQueryString(request.QueryString.Value);
            qs.Set(CONTINUATIONTOKEN_QUERYSTRING_KEY, SerializeContinuationToken(continuationToken));

            var uri = new UriBuilder(request.Path);
            uri.Scheme = (request.Headers.TryGetValue(HEADER_X_FORWARDED_PROTO, out var protoValues) ? protoValues.FirstOrDefault() : uri.Scheme);
            uri.Host = (request.Headers.TryGetValue(HEADER_X_FORWARDED_HOST, out var hostValues) ? hostValues.FirstOrDefault() : uri.Host);
            uri.Port = -1; // remove the port information from URI
            uri.Query = qs.ToString();

            return uri.ToString();
        }

        public static TableContinuationToken GetContinuationToken(HttpRequest request)
        {
            var continuationToken = default(TableContinuationToken);

            if (request.Query.TryGetValue(CONTINUATIONTOKEN_QUERYSTRING_KEY, out var tokens))
            {
                continuationToken = DeserializeContinuationToken(Uri.UnescapeDataString(tokens.First()));
            }

            return continuationToken;
        }

        public PagedEntities(IEnumerable<T> entities)
        {
            Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        [JsonProperty("value")]
        public IEnumerable<T> Entities { get; }

        [JsonProperty("nextLink", NullValueHandling = NullValueHandling.Ignore)]
        public string NextLink { get; set; }
    }
}

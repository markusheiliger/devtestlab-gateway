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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using RDGatewayAPI.Data;
using RDGatewayAPI.Services;

namespace RDGatewayAPI.Functions
{
    public sealed class CreateToken
    {
        private const string USER_OBJECTID_HEADER = "x-ms-client-object-id";

        private static readonly Regex TokenParseExpression = new Regex("(?<key>Host|Port|ExpiresOn)=(?<value>.+?)(?=&)", RegexOptions.Compiled);

        private readonly ITokenService tokenService;

        private static void TrackToken(ICollector<string> collector, Guid correlationId, Guid userId, string token)
        {
            var tokenEntity = new TokenEntity(correlationId)
            {
                UserId = userId
            };

            foreach (Match match in TokenParseExpression.Matches(token))
            {
                var key = match.Groups["key"].Value;
                var value = match.Groups["value"].Value;

                switch (key)
                {
                    case "Host":
                        tokenEntity.Host = value;
                        break;

                    case "Port":
                        tokenEntity.Port = int.Parse(value);
                        break;

                    case "ExpiresOn":
                        tokenEntity.ExpiresOn = int.Parse(value);
                        break;
                }
            }

            collector.Add(tokenEntity.ToJson());
        }

        public CreateToken(ITokenService tokenService)
        {
            this.tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        [FunctionName(nameof(CreateToken))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "host/{host}/port/{port}")] HttpRequest req,
            [Queue("track-token")] ICollector<string> trackTokenQueue,
            ILogger log, ExecutionContext executionContext,
            string host, int port)
        {
            var user = req.Headers.TryGetValue(USER_OBJECTID_HEADER, out var values) ? values.FirstOrDefault() : default;

            if (string.IsNullOrEmpty(user) || !Guid.TryParse(user, out Guid userId))
            {
                log.LogError($"BadRequest - missing or invalid request header '{USER_OBJECTID_HEADER}'");

                return new BadRequestResult();
            }

            try
            {
                var response = new { token = await tokenService.GetTokenAsync(host, port).ConfigureAwait(false) };

                TrackToken(trackTokenQueue, req.GetCorrelationId().GetValueOrDefault(executionContext.InvocationId), userId, response.token);

                return new OkObjectResult(response);
            }
            catch (Exception exc)
            {
                log.LogError($"Failed to process request {executionContext.InvocationId}", exc);

                return new InternalServerErrorResult();
            }
        }
    }
}

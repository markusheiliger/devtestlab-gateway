﻿/* ------------------------------------------------------------------------------------------------
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
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace RDGatewayAPI
{
    public static class Extensions
    {
        private const string CORRELATION_ID_HEADER = "X-Correlation-Id";

        public static string ToJson(this ITableEntity tableEntity)
        {
            if (tableEntity is null)
            {
                throw new System.ArgumentNullException(nameof(tableEntity));
            }

            return JsonConvert.SerializeObject(tableEntity);
        }

        public static Guid? GetCorrelationId(this HttpRequest httpRequest)
        {
            if (httpRequest is null)
            {
                throw new System.ArgumentNullException(nameof(httpRequest));
            }

            if (httpRequest.Headers.TryGetValue(CORRELATION_ID_HEADER, out var correlationIdHeaders) && Guid.TryParse(correlationIdHeaders.First(), out var correlationId))
            {
                return correlationId;
            }

            return null;
        }
    }
}

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDGatewayAPI.Data
{
    public sealed class TokenEntity : TableEntity
    {
        public TokenEntity() : this(Guid.Empty)
        {
            // default constructor used for deserialization
        }

        public TokenEntity(Guid rowKey)
        {
            RowKey = rowKey.ToString();
            PartitionKey = DateTime.UtcNow.ToString("yyyy-MM");
            ETag = "*";
        }

        public string SessionId => RowKey;

        public string UserId { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public int ExpiresOn { get; set; }

    }
}

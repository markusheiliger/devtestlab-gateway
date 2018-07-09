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
    public sealed class UserEntity : TableEntity
    {
        public UserEntity() : this(Guid.Empty)
        {
            // default constructor used for deserialization
        }

        public UserEntity(Guid correlationId)
        {
            RowKey = correlationId.ToString();
            PartitionKey = DateTime.UtcNow.Year.ToString();
            ETag = "*";
        }

        public Guid UserId { get; set; }

        public string UserPrincipalName { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

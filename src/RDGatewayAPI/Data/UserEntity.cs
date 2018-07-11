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

        public UserEntity(Guid rowKey)
        {
            RowKey = rowKey.ToString();
            PartitionKey = DateTime.UtcNow.ToString("yyyy");
            ETag = "*";
        }

        public Guid UserId { get { return Guid.Parse(RowKey); } }

        public string UserPrincipalName { get; set; }

        public string UserDisplayName { get; set; }
    }
}

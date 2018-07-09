using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RDGatewayAPI.Data
{
    public sealed class PagedEntities<T>
        where T : ITableEntity
    {
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDGatewayAPI.Model
{
    public sealed class LabUserResourceId : IResourceId
    {
        public Guid ObjectId { get; set; }

        public string ToResourceId(IResourceId parentResource = null)
        {
            return $"/users/{ObjectId}" + parentResource?.ToResourceId();
        }
    }
}

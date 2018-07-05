using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDGatewayAPI.Model
{
    public sealed class LabVMResourceId : IResourceId
    {
        public Guid SubscriptionId { get; set; }

        public string ResourceGroupName { get; set; }

        public string LabName { get; set; }

        public string VirtualMachineName { get; set; }

        public string ToResourceId(IResourceId parentResource = null)
        {
            return $"subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.DevTestLab/labs/{LabName}/virtualmachines/{VirtualMachineName}" + parentResource?.ToResourceId();
        }
    }
}

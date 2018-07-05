using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDGatewayAPI.Model
{
    public interface IResourceId
    {
        string ToResourceId(IResourceId parentResource = null);
    }
}

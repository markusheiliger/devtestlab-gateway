using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RDGatewayAPI
{
    public static class Extensions
    {
        public static string ToJson(this ITableEntity tableEntity)
        {
            return JsonConvert.SerializeObject(tableEntity);
        }

        public static Guid ToGuid(this string value)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                return guid;
            }

            using (MD5 md5 = MD5.Create())
            {
                return new Guid(md5.ComputeHash(Encoding.Default.GetBytes(value)));
            }
        }
    }
}

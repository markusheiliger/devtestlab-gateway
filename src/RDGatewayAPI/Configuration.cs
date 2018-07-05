using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDGatewayAPI
{
    public static class Configuration
    {
        private static string GetValue(string key, string defaultValue = null)
        {
            var value = Environment.GetEnvironmentVariable(key);

            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static int GetValueAsInt(string key, int defaultValue = default(int))
        {
            var value = GetValue(key);

            return int.TryParse(value, out int parsedValue) ? parsedValue : defaultValue;
        }

        public static string SignCertificateUrl => GetValue("SignCertificateUrl");

        public static int TokenLifetime => GetValueAsInt("TokenLifetime", 60);

    }
}

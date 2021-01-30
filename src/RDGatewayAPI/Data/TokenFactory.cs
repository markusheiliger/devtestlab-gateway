using System;
using System.Configuration;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Newtonsoft.Json;

namespace RDGatewayAPI.Data
{
    internal static class TokenFactory
    {
        private const string MACHINE_TOKEN_PATTERN = "Host={0}&Port={1}&ExpiresOn={2}";
        private const string AUTH_TOKEN_PATTERN = "{0}&Signature=1|SHA256|{1}|{2}";

        private static readonly DateTime PosixBaseTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        private static readonly AzureServiceTokenProvider AzureManagementApiTokenProvider = new AzureServiceTokenProvider();

        public static async Task<X509Certificate2> GetCertificateAsync()
        {
            var signCertificateUrl = default(string);

            try
            {
                signCertificateUrl = Environment.GetEnvironmentVariable("SignCertificateUrl");

                // init a key vault client
                KeyVaultClient keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(AzureManagementApiTokenProvider.KeyVaultTokenCallback));

                // get the base64 encoded secret and decode
                var signCertificateSecret = await keyVaultClient.GetSecretAsync(signCertificateUrl).ConfigureAwait(false);
                var signCertificateBuffer = Convert.FromBase64String(signCertificateSecret.Value);

                // unwrap the json data envelope
                var envelope = JsonConvert.DeserializeAnonymousType(Encoding.UTF8.GetString(signCertificateBuffer), new { data = string.Empty, password = string.Empty });

                // return the certificate
                return new X509Certificate2(Convert.FromBase64String(envelope.data), envelope.password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception exc)
            {
                throw new Exception($"Failed to load certificate from KeyVault by URL '{signCertificateUrl}'", exc);
            }
        }

        public static async Task<string> GetTokenAsync(string host, int port, X509Certificate2 certificate = null)
        {
            certificate ??= await GetCertificateAsync().ConfigureAwait(false);

            // create the machine token and sign the data
            var machineToken = string.Format(CultureInfo.InvariantCulture, MACHINE_TOKEN_PATTERN, host, port, GetPosixLifetime());
            var machineTokenBuffer = Encoding.ASCII.GetBytes(machineToken);
            var machineTokenSignature = certificate.GetRSAPrivateKey().SignData(machineTokenBuffer, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // wrap the machine token
            return string.Format(CultureInfo.InvariantCulture, AUTH_TOKEN_PATTERN, machineToken, certificate.Thumbprint, Uri.EscapeDataString(Convert.ToBase64String(machineTokenSignature)));

            static long GetPosixLifetime()
            {
                DateTime endOfLife;

                var tokenLifetime = Environment.GetEnvironmentVariable("TokenLifetime");

                if (string.IsNullOrEmpty(tokenLifetime))
                {
                    // default lifetime is 1 minute
                    endOfLife = DateTime.UtcNow.AddMinutes(1);
                }
                else
                {
                    try
                    {
                        // parse token lifetime
                        var duration = TimeSpan.Parse(tokenLifetime);

                        // apply lifetime from configuration
                        endOfLife = DateTime.UtcNow.Add(duration);
                    }
                    catch (Exception exc)
                    {
                        throw new ConfigurationErrorsException($"Failed to parse token lifetime '{tokenLifetime}' from configuration", exc);
                    }
                }

                // return lifetime in posix format
                return (long)endOfLife.Subtract(PosixBaseTime).TotalSeconds;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using RDGatewayAPI.Model;

namespace RDGatewayAPI
{
    public static class CreateToken
    {
        private const string AZURE_MANAGEMENT_API = "https://management.azure.com/";
        private const string MACHINE_TOKEN_PATTERN = "Host={0}&Port={1}&ExpiresOn={2}";
        private const string AUTH_TOKEN_PATTERN = "{0}&Signature=1|SHA256|{1}|{2}";

        private static readonly AzureServiceTokenProvider AzureManagementApiTokenProvider = new AzureServiceTokenProvider();
        private static readonly DateTime PosixBaseTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private static async Task<X509Certificate2> GetCertificateAsync(RequestContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            try
            {
                // init a key vault client
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(AzureManagementApiTokenProvider.KeyVaultTokenCallback));

                // get the base64 encoded secret and decode
                var signCertificateSecret = await keyVaultClient.GetSecretAsync(Configuration.SignCertificateUrl);
                var signCertificateBuffer = Convert.FromBase64String(signCertificateSecret.Value);

                context.Log.Info($"Downloaded certificate from KeyVault ({signCertificateBuffer.Length} bytes)");

                // unwrap the json data envelope
                var envelope = JsonConvert.DeserializeAnonymousType(Encoding.UTF8.GetString(signCertificateBuffer), new { data = string.Empty, password = string.Empty });

                // return the certificate
                return new X509Certificate2(Convert.FromBase64String(envelope.data), envelope.password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception exc)
            {
                context.Log.Error(exc.Message, exc);

                throw new Exception($"Failed to load certificate from KeyVault by URL '{Configuration.SignCertificateUrl}'", exc);
            }
        }

        private static async Task<DnsEndPoint> GetRDPEndPointAsync(RequestContext requestContext)
        {
            if (requestContext == null)
            {
                throw new ArgumentNullException(nameof(requestContext));
            }
            
            // request token for Azure communication
            var azureToken = await AzureManagementApiTokenProvider.GetAccessTokenAsync(AZURE_MANAGEMENT_API);

            dynamic computeVmResource = await AZURE_MANAGEMENT_API
                .AppendPathSegment(requestContext.LabVMResourceId.ToResourceId())
                .SetQueryParam("api-version", "2016-05-15")
                .WithOAuthBearerToken(azureToken)
                .GetJsonAsync();

            // loop over the assigned network interfaces
            foreach (var networkInterface in computeVmResource.properties.networkProfile.networkInterfaces)
            {
                dynamic networkInterfaceResource = await AZURE_MANAGEMENT_API
                    .AppendPathSegment((string)networkInterface.id)
                    .SetQueryParam("api-version", "2016-05-15")
                    .WithOAuthBearerToken(azureToken)
                    .GetJsonAsync();

                // loop over the assigned load balancer inboud NAT rules
                foreach (var loadBalancerInboundNatRule in ((IEnumerable<dynamic>)networkInterfaceResource.properties.ipConfigurations).SelectMany(ipConfiguration => ((IEnumerable<dynamic>)ipConfiguration.properties.loadBalancerInboundNatRules)))
                {
                    dynamic loadBalancerInboundNatRuleResource = await AZURE_MANAGEMENT_API
                        .AppendPathSegment((string)loadBalancerInboundNatRule.id)
                        .SetQueryParam("api-version", "2016-05-15")
                        .WithOAuthBearerToken(azureToken)
                        .GetJsonAsync();

                    // if the load balancer inbound NAT rule is pointing to port 3389 in  the backend dig deeper
                    if (loadBalancerInboundNatRuleResource.properties.backendPort == 3389)
                    {
                        dynamic frontendIPConfigurationResource = await AZURE_MANAGEMENT_API
                            .AppendPathSegment((string)loadBalancerInboundNatRuleResource.frontendIPConfiguration.id)
                            .SetQueryParam("api-version", "2016-05-15")
                            .WithOAuthBearerToken(azureToken)
                            .GetJsonAsync();

                        dynamic publicIPAddressResource = await AZURE_MANAGEMENT_API
                            .AppendPathSegment((string)frontendIPConfigurationResource.publicIPAddress.id)
                            .SetQueryParam("api-version", "2016-05-15")
                            .WithOAuthBearerToken(azureToken)
                            .GetJsonAsync();

                        // found all information needed for a RDP connection - return a DNS end point with the gathered information
                        return new DnsEndPoint(publicIPAddressResource.properties.dnsSettings.fqdn, loadBalancerInboundNatRuleResource.properties.frontendPort);
                    }
                }
            }

            throw new Exception($"Could not resolve RDP end point for compute VM '{requestContext.LabVMResourceId.ToResourceId()}'");
        }

        private static async Task<string> GetTokenAsync(RequestContext requestContext, X509Certificate2 certificate)
        {
            if (requestContext == null)
            {
                throw new ArgumentNullException(nameof(requestContext));
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            // request token for Azure communication
            var azureToken = await AzureManagementApiTokenProvider.GetAccessTokenAsync(AZURE_MANAGEMENT_API);

            // fetch the lab VM resource to gather information needed for token creation
            dynamic labVmResource = await AZURE_MANAGEMENT_API
                .AppendPathSegment(requestContext.LabVMResourceId.ToString())
                .SetQueryParam("api-version", "2016-05-15")
                .WithOAuthBearerToken(azureToken)
                .GetJsonAsync();

            // if the lab VM disallows public IP addresses we need to dig deeper to get the required information
            var rdpEndPoint = (DnsEndPoint)(labVmResource.properties.disallowPublicIpAddress
                                ? await GetRDPEndPointAsync(requestContext)
                                : new DnsEndPoint(labVmResource.properties.fqdn, 3389));

            // create the machine token and sign the data
            var machineToken = string.Format(CultureInfo.InvariantCulture, MACHINE_TOKEN_PATTERN, rdpEndPoint.Host, rdpEndPoint.Port, GetPosixLifetime());
            var machineTokenBuffer = Encoding.ASCII.GetBytes(machineToken);
            var machineTokenSignature = certificate.GetRSAPrivateKey().SignData(machineTokenBuffer, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // wrap the machine token
            return string.Format(CultureInfo.InvariantCulture, AUTH_TOKEN_PATTERN, machineToken, certificate.Thumbprint, Uri.EscapeDataString(Convert.ToBase64String(machineTokenSignature)));

            Int64 GetPosixLifetime()
            {
                var endOfLife = DateTime.UtcNow.AddMinutes(Configuration.TokenLifetime);

                return (Int64)endOfLife.Subtract(PosixBaseTime).TotalSeconds;
            }
        }

        [FunctionName("CreateToken")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DevTestLab/labs/{labName}/virtualmachines/{virtualMachineName}/users/{userId}")]HttpRequestMessage req, TraceWriter log, ExecutionContext executionContext, string subscriptionId, string userId, string resourceGroupName, string labName, string virtualMachineName)
        {
            log.Info("C# HTTP trigger function processed a request.");

            RequestContext requestContext;

            try
            {
                var labUserResourceId = new LabUserResourceId
                {
                    ObjectId = Guid.Parse(userId)
                };

                var labVMResourceId = new LabVMResourceId
                {
                    SubscriptionId = Guid.Parse(subscriptionId),
                    ResourceGroupName = resourceGroupName,
                    LabName = labName,
                    VirtualMachineName = virtualMachineName
                };

                requestContext = (new RequestContext(executionContext, log, labVMResourceId, labUserResourceId)).Validate();
            }
            catch (Exception exc)
            {
                log.Error($"Failed to validate request {executionContext.InvocationId}", exc);

                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            try
            {
                // get the signing certificate
                var certificate = await GetCertificateAsync(requestContext);

                // get the signed authentication token
                var response = new { token = await GetTokenAsync(requestContext, certificate) };                

                return req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(response), "application/json");
            }
            catch (Exception exc)
            {
                log.Error($"Failed to process request {executionContext.InvocationId}", exc);

                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        private class RequestContext
        {
            public RequestContext(ExecutionContext executionContext, TraceWriter log, LabVMResourceId labVMResourceId, LabUserResourceId labUserResourceId)
            {
                // init execution context
                ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));

                // init log writer
                Log = log ?? throw new ArgumentNullException(nameof(log));

                LabVMResourceId = labVMResourceId ?? throw new ArgumentNullException(nameof(labVMResourceId));

                LabUserResourceId = labUserResourceId ?? throw new ArgumentNullException(nameof(labUserResourceId));
            }

            public ExecutionContext ExecutionContext { get; }

            public TraceWriter Log { get; }

            public LabVMResourceId LabVMResourceId { get; }

            public LabUserResourceId LabUserResourceId { get; }

            public RequestContext Validate()
            {
                // add validation logic here if needed

                return this;
            }
        }

    }
}

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using RDGatewayAPI.Services;

[assembly: FunctionsStartup(typeof(RDGatewayAPI.Startup))]

namespace RDGatewayAPI
{
    public sealed class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services
                .AddSingleton<ITokenService, TokenService>();
        }
    }
}

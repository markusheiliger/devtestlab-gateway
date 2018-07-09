using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using RDGatewayAPI.Data;

namespace RDGatewayAPI.Functions
{
    public static class TrackUsers
    {
        [FunctionName("TrackUsers")]
        public static async Task Run([QueueTrigger("track-users")]UserEntity user, [Table("users")] CloudTable userTable, TraceWriter log)
        {
            log.Info($"Tacking user '{user.UserId}'");

            var operation = TableOperation.InsertOrReplace(user);

            await userTable.ExecuteAsync(operation).ConfigureAwait(false);
        }
    }
}

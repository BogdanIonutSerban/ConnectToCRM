using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ConnectToCRM.Classes;
using ConnectToCRM.Enums;
using ImportUnionRepresentatives.Services;

namespace ImportUnionRepresentatives
{
    public static class ImportRepresentatives
    {
        [FunctionName("ImportUnionRepresentatives")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string resultMsg;

            try
            {

                resultMsg = ExecuteJob(log);
            }
            catch (Exception ex)
            {
                return new OkObjectResult("ERROR: " + ex.Message);
            }
            return new OkObjectResult(resultMsg);
        }

        public static string ExecuteJob(ILogger log)
        {
            CRM_Logger crmLog = new CRM_Logger();
            CRM_ServiceProvider serviceProvider = new CRM_ServiceProvider();

            try
            {
                string result = "Succes";
                AzureBlobService az = new AzureBlobService();
                var fileString = az.GetFileFromAzure("union-representatives", "UnionRepresentatives.xlsx");

                crmLog.Log(serviceProvider, result, CRM_LogStatus.Successful);

                return result;
            }
            catch (Exception ex)
            {
                crmLog.Log(serviceProvider, ex.Message, CRM_LogStatus.Failed);
                return ex.Message;
            }
        }

    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ConnectToCRM.Services;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;

namespace ConnectToCRM
{
    public static class CreateCRMConnection
    {
        static ServiceClient service;
        [FunctionName("CreateCRMConnection")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            try
            {
                DataverseService dataverseService = new DataverseService();
                service = dataverseService.CreateServiceClient();
                if (service != null)
                {
                    QueryExpression qry = new QueryExpression("account");
                    qry.ColumnSet = new ColumnSet(true);
                    EntityCollection ecAccount = service.RetrieveMultiple(qry);
                    if(ecAccount.Entities.Count > 0)
                    {
                        return new OkObjectResult("Account Count: " + ecAccount.Entities.Count);
                    }
                }

                return new OkObjectResult("Connection Succesfull");
            }
            catch(Exception ex)
            {
                return new OkObjectResult("Error:" + ex.Message);

            }           
        }
    }
}

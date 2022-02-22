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
using ConnectToCRM.Classes;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk.Messages;

namespace ConnectToCRM
{
    public static class GetOrganizationsFromCodeServer
    {
        /* Calling Object: 
         * 
         * {
                "classificationId":"1.2.246.537.6.202",
                "executionType": "DailyUpsert",  //InitialCreate, InitialUpdate, DailyUpsert
                "modifiedAfter":"2022-02-22"
            }
         * */



        static ServiceClient service;
        [FunctionName("GetSoteOrganizations")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            RequestObject requestData = new RequestObject();
            var resultMsg = string.Empty;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                requestData = JsonConvert.DeserializeObject<RequestObject>(requestBody);
                log.LogInformation("Request object parsed sucessfully");
                resultMsg = ExecuteJob(requestData);
            }
            catch(Exception ex)
            {
                return new OkObjectResult("ERROR: " + ex.Message);
            }
            return new OkObjectResult(resultMsg);
        }

        public static string ExecuteJob(RequestObject requestData)
        {
            if (string.IsNullOrEmpty(requestData.ClassificationId))
            {
                return "ClassificationId must not be empty. Job aborted!";
            }

            try
            {
                DataRetrieveManager retriever = new DataRetrieveManager(requestData);
                var response = ConnectToCRM();
                string result = "Succes";
                int pageNo = 1;
                int totalPages = 0;

                do
                {
                    ConceptCodes organisations = retriever.GetOrganizations(pageNo);
                    CRM_ImportManager mngr = new CRM_ImportManager(organisations.ConceptCodes1, service);
                    mngr.Execute(requestData.ExecutionType);

                    totalPages = organisations.TotalPages;
                    pageNo++;
                } while (pageNo < totalPages && pageNo == 2);// remove && pageNo == 2

                return result;
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
        }

        public static string ConnectToCRM()
        {
            try
            {
                DataverseService dataverseService = new DataverseService();
                service = dataverseService.CreateServiceClient();
                if (service != null)
                {
                    QueryExpression qry = new QueryExpression("account");
                    qry.ColumnSet = new ColumnSet(true);
                    EntityCollection ecAccount = service.RetrieveMultiple(qry);
                    if (ecAccount.Entities.Count > 0)
                    {
                        return $"Account Count: {ecAccount.Entities.Count}";
                    }
                }

                return "Connection Succesfull";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";

            }
        }

    }
}

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



        //static ServiceClient service;

         [FunctionName("GetSoteOrganizations")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            RequestObject requestData;
            string resultMsg;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                log.LogInformation($"GetSoteOrganizations Called with request: {requestBody}");
                requestData = JsonConvert.DeserializeObject<RequestObject>(requestBody);
                log.LogInformation("Request object parsed sucessfully");
                resultMsg = ExecuteJob(requestData, log);
            }
            catch(Exception ex)
            {
                return new OkObjectResult("ERROR: " + ex.Message);
            }
            return new OkObjectResult(resultMsg);
        }

        public static string ExecuteJob(RequestObject requestData, ILogger log)
        {
            if (string.IsNullOrEmpty(requestData.ClassificationId))
            {
                log.LogInformation("ClassificationId must not be empty. Job aborted!");
                return "ClassificationId must not be empty. Job aborted!";
            }

            try
            {
                DataRetrieveManager retriever = new DataRetrieveManager(requestData);
                
                string result = "Succes";
                int pageNo = 1;
                int totalPages = 0;
                int insertRecordCouter = 0;
                int updateRecordCouter = 0;

                do
                {
                    ConceptCodes organisations = retriever.GetOrganizations(pageNo);
                    log.LogInformation($"Retrieved from codeserver: {organisations.TotalItems} records");
                    log.LogInformation($"Sending to import page: {organisations.Page} with {organisations.ConceptCodes1.Count} records");
                    CRM_ImportManager mngr = new CRM_ImportManager(organisations.ConceptCodes1, log);
                    ResponseObject execResponse = mngr.Execute(requestData.ExecutionType);

                    totalPages = organisations.TotalPages;
                    pageNo++;
                    insertRecordCouter += execResponse.InsertedCounter;
                    updateRecordCouter += execResponse.UpdatedCounter;
                } while (pageNo < totalPages && pageNo == 2);// remove && pageNo == 2

                result = $"ExecuteJob processed {pageNo-1} pages out of {totalPages} pages with " +
                    $"{insertRecordCouter} records inserted and {updateRecordCouter} records updated";
                return result;
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
        }

    }
}

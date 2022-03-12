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
using ConnectToCRM.Enums;

namespace ConnectToCRM
{
    public static class GetOrganizationsFromCodeServer
    {
        /* Calling Object: 
         * 
         * {
                "classificationId":"1.2.246.537.6.202",
                "executionType": "DailyUpsert",  //InitialCreate, InitialUpdate, DailyUpsert
                "modifiedAfter":"2022-02-22",
                "batchSize":2
            }
         * */



        //static ServiceClient service;
        static string configParamName = "PageNumber";

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
            catch (Exception ex)
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
            CRM_Logger crmLog = new CRM_Logger();
            CRM_ServiceProvider serviceProvider = new CRM_ServiceProvider();
            CRM_HelperMethods.UpdateModifiedAfterParam_FromCRM(serviceProvider, requestData, "ImportLogRecordName");

            try
            {
                DataRetrieveManager retriever = new DataRetrieveManager(requestData);

                string result = "Succes";
                int pageNo = CalculatePageNo(serviceProvider, configParamName) + 1;

                int totalPages = 0;
                int insertRecordCouter = 0;
                int updateRecordCouter = 0;

                do
                {
                    ConceptCodes organisations = retriever.GetOrganizations(pageNo);
                    if (organisations == null)
                        return "NO organizations Retrieved";
                    log.LogInformation($"Retrieved from codeserver: {organisations.TotalItems} records");
                    log.LogInformation($"Sending to import page: {organisations.Page} with {organisations.ConceptCodes1.Count} records");
                    CRM_ImportManager mngr = new CRM_ImportManager(organisations.ConceptCodes1, serviceProvider, log);
                    ResponseObject execResponse = mngr.Execute(requestData.ExecutionType);

                    totalPages = organisations.TotalPages;
                    UpdateLastProcessedPage(serviceProvider, pageNo);
                    pageNo++;
                    insertRecordCouter += execResponse.InsertedCounter;
                    updateRecordCouter += execResponse.UpdatedCounter;

                } while (pageNo < totalPages && pageNo<2);

                result = $"ExecuteJob processed {pageNo - 1} pages out of {totalPages} pages with " +
                    $"{insertRecordCouter} records inserted and {updateRecordCouter} records updated";
                //After execution is succesfull, update CRM parameter with 0
                UpdateLastProcessedPage(serviceProvider, 0);
                crmLog.Log(serviceProvider, result, CRM_LogStatus.Successful);

                return result;
            }
            catch (Exception ex)
            {
                crmLog.Log(serviceProvider, ex.Message, CRM_LogStatus.Failed);
                return ex.Message;
            }
        }

        public static int GetPageLimit(RequestObject requestData, int pageNo)
        {
            int result = 1;
            if (requestData.BatchSize != null && requestData.BatchSize > 0 && requestData.BatchSize <= 100)
            {
                result = (int)requestData.BatchSize;
            }
            return pageNo + result;
        }
        public static int CalculatePageNo(CRM_ServiceProvider serviceProvider, string keyName)
        {
            int result = 0;
            var param = CRM_HelperMethods.GetConfigParamByKey(serviceProvider, keyName);
            if (param.Id != Guid.Empty)
            {
                var resultTxt = param.GetAttributeValue<string>("els_value");
                var success = int.TryParse(resultTxt, out int foundValue);
                if (success && foundValue > 0)
                {
                    result = foundValue;
                }
            }
            return result;
        }

        public static void UpdateLastProcessedPage(CRM_ServiceProvider serviceProvider, int pageNo)
        {
            ExecuteMultipleRequest insertOrUpdateRequests = new ExecuteMultipleRequest()
            {
                Settings = new ExecuteMultipleSettings()
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };

            var param = CRM_HelperMethods.GetConfigParamByKey(serviceProvider, configParamName);
            param["els_value"] = pageNo.ToString();

            var service = serviceProvider.GetService();
            if (param.Id != Guid.Empty)
            {
                UpdateRequest updateRequest = new UpdateRequest { Target = param };
                insertOrUpdateRequests.Requests.Add(updateRequest);
            }
            else
            {
                CreateRequest createRequest = new CreateRequest { Target = param };
                insertOrUpdateRequests.Requests.Add(createRequest);
            }
            ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(insertOrUpdateRequests);
        }

    }
}

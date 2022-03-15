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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk.Messages;
using UpdateHierarchyLevel.Helpers;

namespace UpdateHierarchyLevel
{
    public static class UpdateHierarchyLevel
    {
        static string configParamName = "PageNumber";
        [FunctionName("UpdateHierarchyLevel")]
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
            try
            {
                int recordsUpdated = 0;
                int totalRecords = 0;
                string result = "Succes";
                DataverseService dataverseService = new DataverseService();
                ServiceClient service = dataverseService.CreateServiceClient();
                ExecuteMultipleRequest exeReq = Helper.GetExecuteMultipleReq();
                #region RetrieveConceptCodesWithParent
                QueryExpression qryConceptCodes = new QueryExpression("els_soteorganisaatiorekisteri");
                qryConceptCodes.ColumnSet = new ColumnSet("els_parentid");
                //qryConceptCodes.TopCount = 10;
                qryConceptCodes.Criteria.AddCondition("els_hierarchylevel", ConditionOperator.NotNull);
                List<Entity> ecConceptCodes = Helper.RetrieveAll(qryConceptCodes, service);
                totalRecords = ecConceptCodes.Count;
                foreach (var soteOrg in ecConceptCodes)
                {
                    CalculateHierarchyLevel(soteOrg, exeReq, service, log);
                    recordsUpdated++;
                    if (recordsUpdated%500 == 0)
                    {
                        bool executedSuccessfuly = Helper.ExecuteRequests(exeReq);
                        exeReq.Requests.Clear();
                    }
                }
                #endregion
                
                result = $"ExecuteJob processed {recordsUpdated} records out of " +
                    $"{totalRecords} records";
                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        private static void CalculateHierarchyLevel(Entity entity,ExecuteMultipleRequest exeReq, ServiceClient service, ILogger log)
        {
            int hierarchyLevel = 0;
            if (entity.Contains("els_parentid"))
            {
                EntityReference parent = (EntityReference)entity["els_parentid"];
                string parentId = parent.Id.ToString();
                while (1 == 1)
                {
                    Entity parentEntity = service.Retrieve(parent.LogicalName, Guid.Parse(parentId), new ColumnSet("els_parentid"));
                    if (parentEntity.Contains("els_parentid") && parentEntity["els_parentid"] != null)
                    {
                        hierarchyLevel++;
                        parentId = ((EntityReference)parentEntity["els_parentid"]).Id.ToString();
                    }
                    else
                    {
                        break;
                    }
                }
            }
            log.LogInformation($"Hierarchy Level = {hierarchyLevel}");
            //Update HierarchyLevel field
            entity["els_hierarchylevel"] = hierarchyLevel;
            UpdateRequest updateRequest = new UpdateRequest { Target = entity };
            exeReq.Requests.Add(updateRequest);
        }
    }
}

using ConnectToCRM.Services;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UpdateHierarchyLevel.Helpers
{
    public static class Helper
    {
        public static List<Entity> RetrieveAll(QueryExpression query, ServiceClient service)
        {
            List<Entity> result = new List<Entity>();

            if (!query.TopCount.HasValue)
            {
                if (query.PageInfo == null)
                {
                    query.PageInfo = new PagingInfo();
                }
                if (query.PageInfo.Count == 0)
                {
                    query.PageInfo.Count = 5000;
                }
                if (query.PageInfo.PageNumber == 0)
                {
                    query.PageInfo.PageNumber = 1;
                }
            }

            EntityCollection ecPartial = service.RetrieveMultiple(query);
            result.AddRange(ecPartial.Entities.ToList());

            while (ecPartial.MoreRecords)
            {
                query.PageInfo.PageNumber += 1;
                query.PageInfo.PagingCookie = ecPartial.PagingCookie;
                ecPartial = service.RetrieveMultiple(query);
                result.AddRange(ecPartial.Entities.ToList());
            }

            return result;
        }
        public static ExecuteMultipleRequest GetExecuteMultipleReq()
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
            return insertOrUpdateRequests;
        }

        public static bool ExecuteRequests(ExecuteMultipleRequest exeReq)
        {
            try
            {
                DataverseService ds = new DataverseService();
                var service = ds.CreateServiceClient();
                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(exeReq);
                if (responseWithResults.IsFaulted == true)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

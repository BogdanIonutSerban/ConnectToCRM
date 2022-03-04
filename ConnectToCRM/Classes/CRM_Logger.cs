using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class CRM_Logger
    {
        string logId;
        RequestObject requestData;
        public CRM_Logger(RequestObject _requestData)
        {
            requestData = _requestData;
        }
        public void CreateLog(CRM_ServiceProvider serviceProvider, string details = "")
        {
            ExecuteMultipleRequest insertOrUpdateRequests = GetMultipleRequest();
            Entity logRecord = CreateLogRecord(requestData, details);

            CreateRequest createRequest = new CreateRequest { Target = logRecord };
            insertOrUpdateRequests.Requests.Add(createRequest);

            var service = serviceProvider.GetService();
            ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(insertOrUpdateRequests);
            if (responseWithResults.IsFaulted == false)
            {
                logId = responseWithResults.Responses[0].Response["id"].ToString();
            }
        }
        public void UpdateLog(CRM_ServiceProvider serviceProvider, string details, int statusCode)
        {
            if (!string.IsNullOrEmpty(logId))
            {
                ExecuteMultipleRequest insertOrUpdateRequests = GetMultipleRequest();
                Entity logRecord = UpdateLogRecord(details, statusCode);

                UpdateRequest updateRequest = new UpdateRequest { Target = logRecord };
                insertOrUpdateRequests.Requests.Add(updateRequest);

                var service = serviceProvider.GetService();
                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(insertOrUpdateRequests);
            }

        }
        ExecuteMultipleRequest GetMultipleRequest()
        {
            return new ExecuteMultipleRequest()
            {
                Settings = new ExecuteMultipleSettings()
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };
        }
        Entity CreateLogRecord(RequestObject requestData, string details)
        {
            string logName = $"{requestData.ExecutionType}_{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToShortTimeString()}";
            Entity rec = new Entity("els_codeserver_importlog");
            rec.Attributes.Add("els_name", logName);
            //rec.Attributes.Add("els_details", details);

            return rec;
        }
        Entity UpdateLogRecord (string details, int statuscode)
        {
            Entity rec = new Entity("els_codeserver_importlog");
            rec.Id = new Guid(logId);
            rec.Attributes.Add("statecode", new OptionSetValue(1));
            rec.Attributes.Add("statuscode", new OptionSetValue(statuscode));
            rec.Attributes.Add("els_details", details);

            return rec;
        }
    }
}

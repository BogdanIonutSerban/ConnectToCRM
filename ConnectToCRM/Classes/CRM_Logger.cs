using ConnectToCRM.Enums;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class CRM_Logger
    {
        readonly string configParamName = "ImportLogRecordName";
        public void Log(CRM_ServiceProvider serviceProvider, string details, CRM_LogStatus statusCode)
        {
            var configParam = CRM_HelperMethods.GetConfigParamByKey(serviceProvider, configParamName);
            if (configParam.Id == Guid.Empty)
            {
                string exceptionMsg = $"Could not retrieve ConfigParameter record: {configParamName}! Update of SOTE-organisaatiorekisterin integraation tilanne aborted.";
                throw new Exception(exceptionMsg);
            }
            string logRecordName = configParam.GetAttributeValue<string>("els_value");

            Entity logRecord = CRM_HelperMethods.GetLogRecordFromCRM(serviceProvider, logRecordName);
            if (logRecord.Id == Guid.Empty)
            {
                string exceptionMsg = $"Could not retrieve the import log record: {logRecordName}! Update of SOTE-organisaatiorekisterin integraation tilanne aborted.";
                throw new Exception(exceptionMsg);
            }

            ExecuteMultipleRequest insertOrUpdateRequests = GetMultipleRequest();

            if (statusCode == CRM_LogStatus.Successful)
            {
                UpdateLogRecord_success(details, logRecord);
            }

            if (statusCode == CRM_LogStatus.Failed)
            {
                UpdateLogRecord_error(details, logRecord);
            }

            UpdateRequest updateRequest = new UpdateRequest { Target = logRecord };
            insertOrUpdateRequests.Requests.Add(updateRequest);

            var service = serviceProvider.GetService();
            ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(insertOrUpdateRequests);
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
        void UpdateLogRecord_success (string details, Entity logRecord)
        {
            logRecord["els_successfullyimporteddate"] = DateTime.Now;
            logRecord["els_successfullylog"] = details;
        }
        void UpdateLogRecord_error(string details, Entity logRecord)
        {
            logRecord["els_faileddate"] = DateTime.Now;
            logRecord["els_failedlog"] = details;

        }
    }
}

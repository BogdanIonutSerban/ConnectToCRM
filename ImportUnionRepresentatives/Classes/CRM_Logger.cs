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
        readonly string ConfigParamName = "ImportRepresentativesLogTableName";
        DateTime StartTime;
        string RecordName;
        public int NoOfRecordsFound { get; set; }
        List<string> Details = new List<string>();
        public CRM_Logger()
        {
            StartTime = DateTime.Now;
            RecordName = $"Representatve Import Event {StartTime}";
        }
        public void AddDetailInfo(string detailText)
        {
            Details.Add(detailText);
        }
        public void Log(CRM_ServiceProvider serviceProvider, CRM_LogStatus statusCode)
        {
            var configParam = CRM_HelperMethods.GetConfigParamByKey(serviceProvider, ConfigParamName);
            if (configParam.Id == Guid.Empty)
            {
                string exceptionMsg = $"Could not retrieve ConfigParameter record: {ConfigParamName}! ";
                throw new Exception(exceptionMsg);
            }
            string logTableName = configParam.GetAttributeValue<string>("els_value");

            var logRecord = CreateLogRecord(logTableName, statusCode);
            ExecuteMultipleRequest insertOrUpdateRequests = GetMultipleRequest();

            CreateRequest updateRequest = new CreateRequest { Target = logRecord };
            insertOrUpdateRequests.Requests.Add(updateRequest);

            var service = serviceProvider.GetService();
            ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(insertOrUpdateRequests);
        }
        string ProcessDetails()
        {
            string log = string.Join("\n", Details.ToArray());
            if (log.Length > 10000)
            {
                log = $"{log.Substring(0, 9996)}...";
            }
            return log;
        }
        Entity CreateLogRecord(string logTableName, CRM_LogStatus statusCode)
        {
            Entity logRecord = new Entity(logTableName);
            logRecord["els_date"] = StartTime;
            logRecord["els_name"] = RecordName;
            logRecord["els_log"] = ProcessDetails();
            logRecord["statuscode"] = new OptionSetValue((int)statusCode);
            return logRecord;
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

    }
}

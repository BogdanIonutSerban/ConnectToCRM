using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConnectToCRM.Classes
{
    public static class CRM_HelperMethods
    {
        public static Entity GetConfigParamByKey(CRM_ServiceProvider serviceProvider, string keyName)
        {
            var service = serviceProvider.GetService();
            Entity result = new Entity("els_configurationparameter");
            var query = new QueryExpression("els_configurationparameter")
            {
                ColumnSet = new ColumnSet("els_value")
            };

            query.Criteria.AddCondition("els_name", ConditionOperator.Equal, keyName);

            var response = service.RetrieveMultiple(query);
            if (response != null && response.Entities.Any())
            {
                result = response.Entities.First();
            }
            return result;
        }

        public static void UpdateModifiedAfterParam_FromCRM(CRM_ServiceProvider serviceProvider, string configParamName)
        {
            
        }
        public static Entity GetLogRecordFromCRM(CRM_ServiceProvider serviceProvider, string keyName)
        {
            var service = serviceProvider.GetService();
            Entity result = new Entity("els_soteorgintegrationstatus");
            var query = new QueryExpression("els_soteorgintegrationstatus")
            {
                ColumnSet = new ColumnSet(new string[] { "els_faileddate", "els_failedlog", "els_successfullyimporteddate", "els_successfullylog" })
            };

            query.Criteria.AddCondition("els_name", ConditionOperator.Equal, keyName);

            var response = service.RetrieveMultiple(query);
            if (response != null && response.Entities.Any())
            {
                result = response.Entities.First();
            }
            return result;
        }
    }
}

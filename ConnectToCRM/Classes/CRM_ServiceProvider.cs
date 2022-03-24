using ConnectToCRM.Services;
using Microsoft.PowerPlatform.Dataverse.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class CRM_ServiceProvider
    {
        ServiceClient Service;

        public ServiceClient GetService()
        {
            ValidateCRMConnection();
            return Service;
        }
        void ValidateCRMConnection()
        {
            if (Service is null)
            {
                ConnectToCRM();
            }
            if (!Service.IsReady)
            {
                Service.Dispose();
                ConnectToCRM();
            }
        }
        string ConnectToCRM()
        {
            try
            {
                DataverseService dataverseService = new DataverseService();
                Service = dataverseService.CreateServiceClient();
                /*                if (service != null)
                                {
                                    QueryExpression qry = new QueryExpression("account");
                                    qry.ColumnSet = new ColumnSet(true);
                                    EntityCollection ecAccount = service.RetrieveMultiple(qry);
                                    if (ecAccount.Entities.Count > 0)
                                    {
                                        return $"Account Count: {ecAccount.Entities.Count}";
                                    }
                                }*/

                return "Connection Succesfull";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";

            }
        }
    }
}

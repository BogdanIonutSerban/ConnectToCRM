using ConnectToCRM.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using ConnectToCRM.Classes;
using ConnectToCRM.Services;
using Microsoft.Extensions.Logging;

namespace ConnectToCRM.Classes
{
    public class CRM_ImportManager
    {
        public ICollection<ConceptCode> organisationsCollection { get; private set; }
        public CRM_ServiceProvider serviceProvider { get; private set; }
        CRM_RecordManager recordManager;
        ILogger log;
        public ResponseObject response { get; private set; } 

        public CRM_ImportManager(ICollection<ConceptCode> _collection, CRM_ServiceProvider _serviceProvider, ILogger _log)
        {
            organisationsCollection = _collection;
            log = _log;
            response = new ResponseObject();
            serviceProvider = _serviceProvider;
        }

        public ResponseObject Execute(ExeucutionType executionType)
        {
            if (executionType == ExeucutionType.InitialCreate)
            {
                InsertToCrm();
                return response;
            }
            if (executionType == ExeucutionType.InitialUpdate)
            {
                UpdateToCRM();
                return response;
            }
            if (executionType == ExeucutionType.DailyUpsert)
            {
                UpsertToCRM();
            }

            return response;
        }
        public void InsertToCrm()
        {
            try
            {
                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();
                
                CreateNewrecords(organisationsCollection, exeReq);

                log.LogInformation($"InsertToCrm creating {exeReq.Requests.Count} new records");
                bool executedSuccessfuly = ExecuteRequests(exeReq);
                if (executedSuccessfuly)
                {
                    log.LogInformation($"InsertToCrm Creation Succesfull");
                    response.Message = "Creation Succesfull";
                    response.InsertedCounter = exeReq.Requests.Count;
                }
            }
            catch (Exception ex)
            {
                response.Message = "ERROR:" + ex.Message;
            }
        }

        public void UpdateToCRM()
        {
            try
            {
                List<string> idMainList = organisationsCollection.Select(o => o.ConceptCodeId).ToList();
                EntityCollection existingCRMRecords = QueryCRMForConceptCodeIds(idMainList);
                log.LogInformation($"UpdateToCRM found {existingCRMRecords.Entities.Count} existing records in CRM");

                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();
                UpdateExistingRecords(existingCRMRecords, organisationsCollection, exeReq);
                log.LogInformation($"UpdateToCRM updating {exeReq.Requests.Count} records in CRM");


                bool executedSuccessfuly = ExecuteRequests(exeReq);
                if (executedSuccessfuly)
                {
                    log.LogInformation($"UpdateToCRM Update Succesfull");
                    response.Message = "Update Succesfull";
                    response.UpdatedCounter = exeReq.Requests.Count;
                }
            }
            catch (Exception ex)
            {
                response.Message = "ERROR:" + ex.Message;
            }
        }
        public void UpsertToCRM()
        {
            try
            {
                List<string> idMainList = organisationsCollection.Select(o => o.ConceptCodeId).ToList();

                EntityCollection existingCRMRecords = QueryCRMForConceptCodeIds(idMainList);
                log.LogInformation($"UpsertToCRM found {existingCRMRecords.Entities.Count} existing records in CRM");

                List<string> existingOrgSubList = existingCRMRecords.Entities.Select(e => e.GetAttributeValue<string>("els_organizationid")).ToList();
                var nonExistingOrgSubList = idMainList.Except(existingOrgSubList).ToList();
                log.LogInformation($"UpsertToCRM {nonExistingOrgSubList.Count} are new to CRM");

                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();

                CreateNewrecords(organisationsCollection, nonExistingOrgSubList, exeReq);
                UpdateExistingRecords(existingCRMRecords, organisationsCollection, exeReq);
                log.LogInformation($"UpsertToCRM total insert and update requests: {exeReq.Requests.Count} ");

                bool executedSuccessfuly = ExecuteRequests(exeReq);
                if (executedSuccessfuly)
                {
                    response.InsertedCounter = nonExistingOrgSubList.Count;
                    response.UpdatedCounter = existingOrgSubList.Count;
                    log.LogInformation($"UpsertToCRM Succesfull");
                    response.Message = "UpsertToCRM Succesfull";
                }

            }
            catch (Exception ex)
            {
                response.Message = "ERROR:" + ex.Message;
            }
        }

        public bool ExecuteRequests(ExecuteMultipleRequest exeReq)
        {
            try
            {
                var service = serviceProvider.GetService();
                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(exeReq);
                if (responseWithResults.IsFaulted == true)
                {
                    response.Message = responseWithResults.Responses.FirstOrDefault().Fault.ToString();
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
        public void CreateNewrecords(ICollection<ConceptCode> organisationsCollection, List<string> idList, ExecuteMultipleRequest exeReq)
        {
            var service = serviceProvider.GetService();
            recordManager = new CRM_RecordManager(service, log);
            foreach (var idStr in idList)
            {
                IEnumerable<ConceptCode> orgList = organisationsCollection.Where(o => o.ConceptCodeId == idStr);
                ConceptCode org = orgList.FirstOrDefault();
                log.LogInformation($"CreateNewrecords creating CreateRequest for : {org.ClassificationId} ");

                Entity newCrmOrg = recordManager.CreateNewCRMRecord(org);

                CreateRequest createRequest = new CreateRequest { Target = newCrmOrg };
                exeReq.Requests.Add(createRequest);
                //if (i == 1)//execute few inserts
                //    break;
            }
        }
        public void CreateNewrecords(ICollection<ConceptCode> organisationsCollection, ExecuteMultipleRequest exeReq)
        {
            var service = serviceProvider.GetService();
            recordManager = new CRM_RecordManager(service, log);
            foreach (var org in organisationsCollection)
            {
                log.LogInformation($"CreateNewrecords creating CreateRequest for : {org.ClassificationId} ");

                Entity newCrmOrg = recordManager.CreateNewCRMRecord(org);

                CreateRequest createRequest = new CreateRequest { Target = newCrmOrg };
                exeReq.Requests.Add(createRequest);
                //if (i == 1)//execute few inserts
                //    break;
            }
        }
        public void UpdateExistingRecords(EntityCollection existingOrganisations, ICollection<ConceptCode> organisationsCollection, ExecuteMultipleRequest exeReq)
        {
            var service = serviceProvider.GetService();
            recordManager = new CRM_RecordManager(service, log);
            foreach (Entity existingOrg in existingOrganisations.Entities)
            {
                var ConceptCodeId = existingOrg.GetAttributeValue<string>("els_organizationid");
                var org = organisationsCollection.Where(c => c.ConceptCodeId.Equals(ConceptCodeId)).FirstOrDefault();
                log.LogInformation($"UpdateExistingRecords creating UpdateRequest for : {org.ClassificationId} ");

                recordManager.UpdateExistingCRMRecord(existingOrg, org);

                UpdateRequest updateRequest = new UpdateRequest { Target = existingOrg };
                exeReq.Requests.Add(updateRequest);
            }
        }
        public EntityCollection QueryCRMForConceptCodeIds(List<string> idMainList)
        {
            var service = serviceProvider.GetService();
            EntityCollection result = new EntityCollection();
            var query = new QueryExpression("els_soteorganisaatiorekisteri");
            query.ColumnSet.AllColumns = true;

            query.Criteria.AddCondition("els_organizationid", ConditionOperator.In, idMainList.ToArray());

            var response = service.RetrieveMultiple(query);
            if (response != null)
            {
                result = response;
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

    }
}

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

namespace ConnectToCRM.Classes
{
    public class CRM_ImportManager
    {
        public ICollection<ConceptCode> organisationsCollection { get; private set; }
        public static ServiceClient service { get; private set; }
        CRM_RecordManager recordManager;

        public CRM_ImportManager(ICollection<ConceptCode> _collection, ServiceClient _service)
        {
            organisationsCollection = _collection;
            service = _service;
            recordManager = new CRM_RecordManager(service);
        }

        public string Execute(ExeucutionType executionType)
        {
            string response = "";
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
        public string InsertToCrm()
        {
            try
            {
                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();
                
                CreateNewrecords(organisationsCollection, exeReq);
                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(exeReq);

                if (responseWithResults.IsFaulted == true)
                {
                    return responseWithResults.Responses.FirstOrDefault().Fault.ToString();
                }
                else
                {
                    return "Creation Succesfull";
                }
            }
            catch (Exception ex)
            {
                return "ERROR:" + ex.Message;
            }
        }

        public string UpdateToCRM()
        {
            try
            {
                List<string> idMainList = organisationsCollection.Select(o => o.ConceptCodeId).ToList();
                EntityCollection existingCRMRecords = QueryCRMForConceptCodeIds(idMainList);

                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();
                UpdateExistingRecords(existingCRMRecords, organisationsCollection, exeReq);

                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(exeReq);
                if (responseWithResults.IsFaulted == true)
                {
                    return responseWithResults.Responses.FirstOrDefault().Fault.ToString();
                }
                else
                {
                    return "Update Successful";
                }
            }
            catch (Exception ex)
            {
                return "ERROR:" + ex.Message;
            }
        }
        public string UpsertToCRM()
        {
            try
            {
                List<string> idMainList = organisationsCollection.Select(o => o.ConceptCodeId).ToList();

                EntityCollection existingCRMRecords = QueryCRMForConceptCodeIds(idMainList);
                List<string> existingOrgSubList = existingCRMRecords.Entities.Select(e => e.GetAttributeValue<string>("els_organizationid")).ToList();
                var nonExistingOrgSubList = idMainList.Except(existingOrgSubList).ToList();

                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();

                //if initial import was not done create all entities else do update logic
                CreateNewrecords(organisationsCollection, nonExistingOrgSubList, exeReq);
                UpdateExistingRecords(existingCRMRecords, organisationsCollection, exeReq);

                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(exeReq);
                if (responseWithResults.IsFaulted == true)
                {
                    return responseWithResults.Responses.FirstOrDefault().Fault.ToString();
                }
                else
                {
                    return "Upsert Succesfull";
                }
            }
            catch (Exception ex)
            {
                return "ERROR:" + ex.Message;
            }
        }
        public void CreateNewrecords(ICollection<ConceptCode> organisationsCollection, List<string> idList, ExecuteMultipleRequest exeReq)
        {
            int i = 1;
            foreach (var idStr in idList)
            {
                IEnumerable<ConceptCode> orgList = organisationsCollection.Where(o => o.ConceptCodeId == idStr);
                ConceptCode org = orgList.FirstOrDefault();
                Entity newCrmOrg = recordManager.CreateNewCRMRecord(org);

                CreateRequest createRequest = new CreateRequest { Target = newCrmOrg };
                exeReq.Requests.Add(createRequest);
                if (i == 1)//execute few inserts
                    break;
            }
        }
        public void CreateNewrecords(ICollection<ConceptCode> organisationsCollection, ExecuteMultipleRequest exeReq)
        {
            int i = 1;
            foreach (var org in organisationsCollection)
            {
                Entity newCrmOrg = recordManager.CreateNewCRMRecord(org);

                CreateRequest createRequest = new CreateRequest { Target = newCrmOrg };
                exeReq.Requests.Add(createRequest);
                if (i == 1)//execute few inserts
                    break;
            }
        }
        public void UpdateExistingRecords(EntityCollection existingOrganisations, ICollection<ConceptCode> organisationsCollection, ExecuteMultipleRequest exeReq)
        {
            foreach (Entity existingOrg in existingOrganisations.Entities)
            {
                var ConceptCodeId = existingOrg.GetAttributeValue<string>("els_organizationid");
                var org = organisationsCollection.Where(c => c.ConceptCodeId.Equals(ConceptCodeId)).FirstOrDefault();
                recordManager.UpdateExistingCRMRecord(existingOrg, org);

                UpdateRequest updateRequest = new UpdateRequest { Target = existingOrg };
                exeReq.Requests.Add(updateRequest);
            }
        }
        public static EntityCollection QueryCRMForConceptCodeIds(List<string> idMainList)
        {
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

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

namespace ConnectToCRM
{
    public static class GetOrganizationsFromCodeServer
    {
        static ServiceClient service;
        [FunctionName("GetSoteOrganizations")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            try
            {
                var result = ExecuteJob();

                return new OkObjectResult(result);
            }
            catch(Exception ex)
            {

                return new OkObjectResult("ERROR: " + ex.Message);
            }
        }

        public static string ExecuteJob()
        {
            try
            {
                string result = "Succes";
                string id = "1.2.246.537.6.202";
                int pageNo = 1;
                int totalPages = 0;
                int count = 0;

                do
                {
                    result = GetOrganisationsFromCodeserver(id, pageNo);
                    ConceptCodes organisations = JsonConvert.DeserializeObject<ConceptCodes>(result);
                    result = UpsertToCRM(organisations.ConceptCodes1);
                    totalPages = organisations.TotalPages;
                    pageNo++;
                } while (pageNo < totalPages && pageNo == 2);

                return result;
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
        }
        public static string GetOrganisationsFromCodeserver(string id, int pageNo)
        {
            string response = string.Empty;
            WebServiceDefinition webServiceDefinition = new WebServiceDefinition()
            {
                EndpointAddress = $"https://koodistopalvelu.kanta.fi/codeserver/csapi/v3/classifications/{id}/conceptcodes?<queryParams>",
                MessageVerb = Enums.Verb.GET,
                UseAuth = false,
                Timeout = 60,
                Headers = new Dictionary<string, string>()
            };

            WebSeviceCall webSeviceCall;
            try
            {
                string queryParams = $"status=ACTIVE&sortBy=CONCEPTCODEID&pageSize=500&page={pageNo}";
                webSeviceCall = new WebSeviceCall(webServiceDefinition, queryParams);


                webSeviceCall.PayloadJson = string.Empty;
            }
            catch (Exception ex)
            {
                return $"Error at creating WebSeviceCall: {ex.Message}";
            }

            try
            {
                var result = webSeviceCall.GetResult();

                response = result.ResultObject;
            }
            catch (Exception ex)
            {
                return $"Error on calling Web Service !{Environment.NewLine}Error: {ex.Message}";
            }
            return response;
        }
        public static string UpsertToCRM(ICollection<ConceptCode> organisationsCollection)
        {            
            try
            {
                List<string> idMainList = organisationsCollection.Select(o => o.ConceptCodeId).ToList();                
                var response = ConnectToCRM();

                EntityCollection existingCRMRecords = QueryCRMForConceptCodeIds(idMainList);
                List<string> existingOrgSubList = existingCRMRecords.Entities.Select(e => e.GetAttributeValue<string>("els_organizationid")).ToList();
                var nonExistingOrgSubList = idMainList.Except(existingOrgSubList).ToList();

                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();

                //if initial import was not done create all entities else do update logic
                if(existingCRMRecords.Entities.Count == 0)
                    CreateNewrecords(organisationsCollection, nonExistingOrgSubList, exeReq);
                else 
                    UpdateExistingRecords(existingCRMRecords, organisationsCollection, exeReq);

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
            catch(Exception ex)
            {
                return "ERROR:" + ex.Message;
            }
        }
        public static void UpdateExistingRecords(EntityCollection existingOrganisations, ICollection<ConceptCode> organisationsCollection, ExecuteMultipleRequest exeReq)
        {
            foreach (Entity existingOrg in existingOrganisations.Entities)
            {
                var ConceptCodeId = existingOrg.GetAttributeValue<string>("els_organizationid");
                var org = organisationsCollection.Where(c => c.ConceptCodeId.Equals(ConceptCodeId)).FirstOrDefault();
                UpdateExistingCRMRecord(existingOrg, org);

                UpdateRequest updateRequest = new UpdateRequest { Target = existingOrg };
                exeReq.Requests.Add(updateRequest);
            }
        }
        public static void CreateNewrecords(ICollection<ConceptCode> organisationsCollection, List<string> idList, ExecuteMultipleRequest exeReq)
        {
            int i = 1;
            foreach (var idStr in idList)
            {
                IEnumerable<ConceptCode> orgList = organisationsCollection.Where(o => o.ConceptCodeId == idStr);
                ConceptCode org = orgList.FirstOrDefault();
                Entity newCrmOrg = CreateNewCRMRecord(org);

                CreateRequest createRequest = new CreateRequest { Target = newCrmOrg };
                exeReq.Requests.Add(createRequest);
                if (i == 1)//execute few inserts
                    break;
            }
        }
        public static void UpdateExistingCRMRecord(Entity existingOrg, ConceptCode org)
        {
            var attributes = org.Attributes.ToList();

            var longName = attributes.Where(c => c.AttributeName.Equals("LongName"));

            var parentId = attributes.Where(c => c.AttributeName.Equals("ParentId"));
            Entity parentRec = GetConceptCodeRef_ByConceptCodeID(parentId.First().AttributeValue.FirstOrDefault());
            if (parentRec.Id != Guid.Empty)
            {
                existingOrg["els_parentid"] = parentRec.ToEntityReference();
            }
            Dictionary<string, string> mappings = GetMappings();
            foreach (var attr in org.Attributes)
            {
                if (mappings.ContainsKey(attr.AttributeName))
                {
                    Console.WriteLine("AttributeName:" + attr.AttributeName);
                    if (attr.AttributeName == "Sektori")
                    {
                        existingOrg[mappings[attr.AttributeName]] = GetSektoriOptionSetByLabel(attr.AttributeValue.FirstOrDefault());
                    }
                    else if (attr.AttributeName == "Sos.palveluyksikkö" || attr.AttributeName == "Sos.toimintayksikkö" || attr.AttributeName == "Terv.palveluyksikkö")
                    {
                        //get values for boolean fields
                        existingOrg[mappings[attr.AttributeName]] = GetBooleanFromString(attr.AttributeValue.FirstOrDefault());

                    }
                    else if (attr.AttributeName == "ParentId")
                    {
                        existingOrg[mappings[attr.AttributeName]] = GetEntityByName("els_soteorganisaatiorekisteri", attr.AttributeValue.FirstOrDefault(), "els_organizationid", service);


                    }
                    else if (attr.AttributeName == "Sijainti kunta")
                    {
                        existingOrg[mappings[attr.AttributeName]] = GetEntityByName("els_koodi", attr.AttributeValue.FirstOrDefault(), "els_koodinnimi", service);
                    }
                    else
                    {
                        existingOrg[mappings[attr.AttributeName]] = attr.AttributeValue.FirstOrDefault();//for string fields only get value
                    }
                }
            }

            existingOrg["els_longname"] = $"UPDATED_{longName.First().AttributeValue.FirstOrDefault()}";
        }
        public static Entity CreateNewCRMRecord(ConceptCode org)
        {
            Entity newOrg = new Entity("els_soteorganisaatiorekisteri");
            newOrg["els_organizationid"] = org.ConceptCodeId;
            newOrg["els_beginningdate"] = org.BeginDate.DateTime;
            newOrg["els_expiringdate"] = org.ExpirationDate.DateTime;
            Dictionary<string, string> mappings = GetMappings();
            foreach (var attr in org.Attributes)
            {
                if (mappings.ContainsKey(attr.AttributeName))
                {
                    Console.WriteLine("AttributeName:" + attr.AttributeName);
                    if (attr.AttributeName == "Sektori")
                    {
                        newOrg[mappings[attr.AttributeName]] = GetSektoriOptionSetByLabel(attr.AttributeValue.FirstOrDefault());
                    }
                    else if (attr.AttributeName == "Sos.palveluyksikkö" || attr.AttributeName == "Sos.toimintayksikkö" || attr.AttributeName == "Terv.palveluyksikkö")
                    {
                        //get values for boolean fields
                       newOrg[mappings[attr.AttributeName]] = GetBooleanFromString(attr.AttributeValue.FirstOrDefault());

                    }
                    else if (attr.AttributeName == "ParentId")
                    {
                        newOrg[mappings[attr.AttributeName]] = GetEntityByName("els_soteorganisaatiorekisteri", attr.AttributeValue.FirstOrDefault(), "els_organizationid", service);


                    }
                    else if (attr.AttributeName == "Sijainti kunta")
                    {
                        newOrg[mappings[attr.AttributeName]] = GetEntityByName("els_koodi", attr.AttributeValue.FirstOrDefault(), "els_koodinnimi", service);
                    }
                    else
                    {
                        newOrg[mappings[attr.AttributeName]] = attr.AttributeValue.FirstOrDefault();//for string fields only get value
                    }
                }
            }
            return newOrg;
        }
        public static Entity GetConceptCodeRef_ByConceptCodeID(string conceptCodeID)
        {
            Entity result = new Entity();
            var query = new QueryExpression("els_soteorganisaatiorekisteri");
            query.ColumnSet = new ColumnSet("els_organizationid");

            query.Criteria.AddCondition("els_organizationid", ConditionOperator.Equal, conceptCodeID);
            var response = service.RetrieveMultiple(query);
            if (response != null && response.Entities.Any())
            {
                result = response.Entities.First();
            }
            return result;
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
        public static OkObjectResult ConnectToCRM()
        {
            try
            {
                DataverseService dataverseService = new DataverseService();
                service = dataverseService.CreateServiceClient();
                if (service != null)
                {
                    QueryExpression qry = new QueryExpression("account");
                    qry.ColumnSet = new ColumnSet(true);
                    EntityCollection ecAccount = service.RetrieveMultiple(qry);
                    if (ecAccount.Entities.Count > 0)
                    {
                        return new OkObjectResult("Account Count: " + ecAccount.Entities.Count);
                    }
                }

                return new OkObjectResult("Connection Succesfull");
            }
            catch (Exception ex)
            {
                return new OkObjectResult("Error:" + ex.Message);

            }
        }

        public static Dictionary<string, string> GetMappings()
        {
            Dictionary<string, string> mappings = new Dictionary< string, string>();
            mappings.Add("Abbreviation", "els_abbreviation");//Item1 = CodeServerField ; Item2= CRMField
            mappings.Add("LongName", "els_longname");
            mappings.Add("ParentId", "els_parentid");
            mappings.Add("PostAddress", "els_postaddress");
            mappings.Add("StreetAddress", "els_streetaddress");
            mappings.Add("PostNumber", "els_postnumber");
            mappings.Add("PostOffice", "els_postoffice");
            mappings.Add("Sektori", "els_sektori");
            mappings.Add("Sijainti kunta", "els_sijaintikunta");
            mappings.Add("Sos.palveluyksikkö", "els_sospalyksikko");
            mappings.Add("Sos.toimintayksikkö", "els_sostoimintayksikko");
            mappings.Add("Terv.palveluyksikkö", "els_tervpalveluyksikko");
            mappings.Add("TOPI-koodi", "els_topikoodi");
            mappings.Add("TOPI-nimi", "els_topinimi");
            mappings.Add("Y-Tunnus", "els_ytunnus");

            return mappings;
        }

        public static bool GetBooleanFromString(string str)
        {
            if(str == "T")
            {
                return true;
            }
            return false;
        }

        //Gets entity reference lookup based on the ConditionField 
        public static EntityReference GetEntityByName(string entityName, string name, string conditionField, IOrganizationService service)
        {
            QueryExpression qryEntity = new QueryExpression(entityName);
            qryEntity.Criteria.AddCondition(conditionField, ConditionOperator.Equal, name);

            EntityCollection ecEntity = service.RetrieveMultiple(qryEntity);

            if(ecEntity.Entities.Count > 0)
            {
                return ecEntity.Entities[0].ToEntityReference();
            }
            return null;
        }


        /*
        Options:
            861120000: 1 Julkinen
            861120001: 2 Yksityinen
            861120002: 3 Yksityinen itseilmoitettu yksikkö
        */
        public static OptionSetValue GetSektoriOptionSetByLabel(string label)
        {
            if(label == "2 Yksityinen")
            {
                return new OptionSetValue(861120001);
            }
            else if (label == "3 Yksityinen itseilmoitettu yksikkö")
            {
                return new OptionSetValue(861120002);
            }
            else
            {
                return new OptionSetValue(861120000);
            }
        }
    }
}

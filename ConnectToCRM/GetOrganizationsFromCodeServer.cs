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
                  ExecuteJob();

                return new OkObjectResult("Success!");
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
                    UpsertToCRM(organisations.ConceptCodes1);
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
                EntityCollection existingCRMRecords = new EntityCollection();
                var response = ConnectToCRM();

                var existingOrgSubList = GetExistingOrganizations(idMainList, existingCRMRecords);
                var nonExistingOrgSubList = idMainList.Except(existingOrgSubList).ToList();

                ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();
                CreateNewrecords(organisationsCollection, nonExistingOrgSubList, exeReq);
                //TODO Add method for Update for existingCRMRecords
                //TODO ExecuteThe Requests
                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)service.Execute(exeReq);
                return "Creation Succesfull";
            }
            catch(Exception ex)
            {
                return "ERROR:" + ex.Message;
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
                if (i == 1)//execute only once
                    break;
            }
        }
        public static Entity CreateNewCRMRecord(ConceptCode org)
        {
            Entity newOrg = new Entity("els_soteorganisaatiorekisteri");
            newOrg["els_organizationid"] = org.ClassificationId;
            newOrg["els_beginningdate"] = org.BeginDate.DateTime;
            newOrg["els_expiringdate"] = org.ExpirationDate.DateTime;
            //TODO add attributes from mappings
            Dictionary<string, string> mappings = GetMappings();
            foreach(var attr in org.Attributes)
            {
                if(mappings.ContainsKey(attr.AttributeName))
                newOrg[mappings[attr.AttributeName]] = attr.AttributeValue.First();
            }
          
            return newOrg;
        }
        public static List<string> GetExistingOrganizations(List<string> idMainList, EntityCollection existingCRMRecords)
        {
            List<string> result = new List<string>();
            var existingRecords = QueryCRMForConceptCodeIds(idMainList);
            if (existingRecords.Entities.Any())
            {
                existingCRMRecords = existingRecords;
                result = existingRecords.Entities.Select(e => e.GetAttributeValue<string>("els_organizationid")).ToList();
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
            mappings.Add("Sos.toimintayksikkö", "sostoimintayksikko");
            mappings.Add("Terv.palveluyksikkö", "els_tervpalveluyksikko");
            mappings.Add("TOPI-koodi", "els_topikoodi");
            mappings.Add("TOPI-nimi", "els_topinimi");
            mappings.Add("Y-Tunnus", "els_ytunnus");

            return mappings;
        }
    }
}

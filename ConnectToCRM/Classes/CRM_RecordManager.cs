using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class CRM_RecordManager
    {
        public static ServiceClient service { get; private set; }
        ILogger log;

        public CRM_RecordManager( ServiceClient _service, ILogger _log)
        {
            service = _service;
            log = _log;
        }

        public Entity CreateNewCRMRecord(ConceptCode retrievedOrg)
        {
            Entity newOrg = new Entity("els_soteorganisaatiorekisteri");
            newOrg["els_organizationid"] = retrievedOrg.ConceptCodeId;
            newOrg["els_beginningdate"] = retrievedOrg.BeginDate.DateTime;
            newOrg["els_expiringdate"] = retrievedOrg.ExpirationDate.DateTime;
            Dictionary<string, string> mappings = GetMappings();
            foreach (var attr in retrievedOrg.Attributes)
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

        public void UpdateExistingCRMRecord(Entity existingOrg, ConceptCode retrievedOrg)
        {
            var attributes = retrievedOrg.Attributes.ToList();

            var longName = attributes.Where(c => c.AttributeName.Equals("LongName"));

            var parentId = attributes.Where(c => c.AttributeName.Equals("ParentId"));
            Entity parentRec = GetConceptCodeRef_ByConceptCodeID(parentId.First().AttributeValue.FirstOrDefault());
            if (parentRec.Id != Guid.Empty)
            {
                existingOrg["els_parentid"] = parentRec.ToEntityReference();
            }
            Dictionary<string, string> mappings = GetMappings();
            foreach (var attr in retrievedOrg.Attributes)
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

        public Entity GetConceptCodeRef_ByConceptCodeID(string conceptCodeID)
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

        public static Dictionary<string, string> GetMappings()
        {
            Dictionary<string, string> mappings = new Dictionary<string, string>();
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
            if (str == "T")
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

            if (ecEntity.Entities.Count > 0)
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
            if (label == "2 Yksityinen")
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

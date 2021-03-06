using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ConnectToCRM.Classes;
using ConnectToCRM.Enums;
using ImportUnionRepresentatives.Services;
using System.Data;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;
using Microsoft.Xrm.Sdk.Messages;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ImportUnionRepresentatives
{
    public static class ImportRepresentatives
    {
        [FunctionName("ImportUnionRepresentatives")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string resultMsg;

            try
            {
                resultMsg = ExecuteJob(log);
            }
            catch (Exception ex)
            {
                return new OkObjectResult("ERROR: " + ex.Message);
            }
            return new OkObjectResult(resultMsg);
        }

        public static string ExecuteJob(ILogger log)
        {
            CRM_Logger crmLog = new CRM_Logger();
            CRM_ServiceProvider serviceProvider = new CRM_ServiceProvider();
            AzureBlobService az = new AzureBlobService();

            try
            {               
                string result = "Success";
                var fileString = az.GetFileFromAzure("union-representatives", "Representatives.csv");
                var table = ConvertToDatatable(fileString, crmLog);
                //var reqList = ProcessDataTableContent(table, crmLog, serviceProvider);
                var reqList = ProcessDataTableContentV2(table, crmLog, serviceProvider);
                string executedSuccessfuly = SendToCRM(reqList, crmLog, serviceProvider);

                //string executedSuccessfuly = ExecuteRequests(reqList, serviceProvider);
                if (executedSuccessfuly != "OK")
                {
                    result = executedSuccessfuly;
                    crmLog.AddDetailInfo($"Execution ended at {DateTime.Now}, with following result: {result}");
                    crmLog.Log(serviceProvider, CRM_LogStatus.Failed);

                    az.MoveProcessedFileToArchive("union-representatives", "union-representatives-failed", "Representatives.csv");
                    return result;
                }
                az.MoveProcessedFileToArchive("union-representatives", "union-representatives-archive", "Representatives.csv");
                crmLog.AddDetailInfo($"Execution ended at {DateTime.Now}, with following result: {result}");
                crmLog.Log(serviceProvider, CRM_LogStatus.Successful);

                return result;
            }
            catch (Exception ex)
            {
                crmLog.AddDetailInfo(ex.Message);
                crmLog.Log(serviceProvider, CRM_LogStatus.Failed);
                az.MoveProcessedFileToArchive("union-representatives", "union-representatives-failed", "Representatives.csv");
                return ex.Message;
            }
        }
        public static DataTable ConvertToDatatable(string fileContent, CRM_Logger crmLog)
        {
            DataTable resultTable = new DataTable();
            string[] allRows = fileContent.Split('\n');
            for (int i = 0; i < allRows.Length - 1; i++)
            {
                crmLog.NoOfRecordsFound = allRows.Length - 1;
                Regex csvParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
                string[] rowValues = csvParser.Split(allRows[i]);
                if (i==0)
                {
                    for (int j = 0; j < rowValues.Length; j++)
                    {
                        resultTable.Columns.Add(rowValues[j]);
                    }
                }
                else
                {
                    if (rowValues.Length != 16)
                    {
                        continue; //TODO Log this Event
                    }
                    DataRow row = resultTable.NewRow();
                    for (int k = 0; k < rowValues.Length; k++)
                    {
                        row[k] = rowValues[k].ToString();
                    }
                    resultTable.Rows.Add(row);
                }
            }

            return resultTable;
        }
        public static ExecuteMultipleRequest ProcessDataTableContentV2(DataTable table, CRM_Logger crmLog, CRM_ServiceProvider serviceProvider)
        {
            int updatesCounter = 0;
            int insertsCounter = 0;
            int batchLimit = 0;

            ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();
            foreach (DataRow row in table.Rows)
            {
                //if commented do full import
                /* if (batchLimit == 205)
                 {
                     break;
                 }*/
                var recKey = row["hetu"].ToString();
                Entity record = GetLuottamusmiesRecord(recKey, serviceProvider);

                foreach (DataColumn column in table.Columns)
                {
                    string columnName = column.ColumnName;
                    string columnData = row[column].ToString();
                    if (column.Ordinal == 0 && record.Id == Guid.Empty)//columnName.Equals("hetu")
                    {
                        var contact = GetRecord("contact", columnData, "els_hetu", serviceProvider.GetService());
                        if (contact != null)
                        {
                            record.Attributes.Add("els_henkilo", contact);
                        }
                        else
                        {
                            var info = $"No Contact was found for Representative {row["sukunimi"]} {row["etunimi"]}";
                            crmLog.AddDetailInfo(info);
                        }
                    }
                }
                #region Mappings
                record.Attributes.Add("els_luottamusmiesnimi", row["yrityksen_nimi"].ToString() + " " + row["etunimi"].ToString() + " " + row["sukunimi"].ToString());
                record.Attributes.Add("els_jasenjarjesto", row["jasenjarjesto"].ToString());
                if (row["alkupvm"].ToString() != string.Empty)
                    record.Attributes.Add("els_alkupaivamaara", DateTime.ParseExact(row["alkupvm"].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture));
                if (row["loppupvm"].ToString() != string.Empty)
                    record.Attributes.Add("els_paattymispaivamaara", DateTime.ParseExact(row["loppupvm"].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture));
                record.Attributes.Add("els_luottamustehtava", new OptionSetValue(GetTehtavaOptionSetValue(row["tehtava"].ToString())));
                if (row["paatoiminen"].ToString() == "E")
                {
                    record.Attributes.Add("els_paatoiminen", false);
                }
                else
                {
                    record.Attributes.Add("els_paatoiminen", true);
                }
                record.Attributes.Add("els_sopimusalat", row["sopimusalat"].ToString());
                record.Attributes.Add("els_sektori", row["yrityksen_sektori"].ToString());
                record.Attributes.Add("els_juko_id", row["ID\r"].ToString().Replace("\r", ""));
                record.Attributes.Add("els_soteorganisaatio", GetSoteOrganization("els_soteorganisaatiorekisteri", row["yrityksen_ytunnus"].ToString(), "els_ytunnus", serviceProvider.GetService()));

                #endregion

                if (record.Id == Guid.Empty)
                {
                    CreateRequest createRequest = new CreateRequest { Target = record };
                    exeReq.Requests.Add(createRequest);
                    insertsCounter++;
                }
                else
                {
                    UpdateRequest updateRequest = new UpdateRequest { Target = record };
                    exeReq.Requests.Add(updateRequest);
                    updatesCounter++;
                }
                batchLimit++;
            }
            crmLog.AddDetailInfo($"This session were inserted {insertsCounter} records and updated {updatesCounter}");
            return exeReq;
        }
        public static List<ExecuteMultipleRequest> ProcessDataTableContent(DataTable table, CRM_Logger crmLog, CRM_ServiceProvider serviceProvider)
        {
            int updatesCounter = 0;
            int insertsCounter = 0;
            int batchLimit = 0;
            List<ExecuteMultipleRequest> multipleReqList = new List<ExecuteMultipleRequest>();
            ExecuteMultipleRequest exeReq = GetExecuteMultipleReq();
            Dictionary<string, string> mappings = GetMappings();
            foreach (DataRow row in table.Rows)
            {
                if (batchLimit % 200 == 0)
                {
                    multipleReqList.Add(exeReq);
                    exeReq.Requests.Clear();
                }
                //if commented do full import
                /* if (batchLimit == 205)
                 {
                     break;
                 }*/
                var recKey = row["hetu"].ToString();
                Entity record = GetLuottamusmiesRecord(recKey, serviceProvider);

                foreach (DataColumn column in table.Columns)
                {
                    string columnName = column.ColumnName;
                    string columnData = row[column].ToString();
                    if (column.Ordinal == 0 && record.Id == Guid.Empty)//columnName.Equals("hetu")
                    {
                        var contact = GetRecord("contact", columnData, "els_hetu", serviceProvider.GetService());
                        if (contact != null)
                        {
                            record.Attributes.Add("els_henkilo", contact);
                        }
                        else
                        {
                            var info = $"No Contact was found for Representative {row["sukunimi"]} {row["etunimi"]}";
                            crmLog.AddDetailInfo(info);
                        }
                    }
                }
                #region Mappings
                record.Attributes.Add("els_luottamusmiesnimi", row["yrityksen_nimi"].ToString() + " " + row["etunimi"].ToString() + " " + row["sukunimi"].ToString());
                record.Attributes.Add("els_jasenjarjesto", row["jasenjarjesto"].ToString());
                if(row["alkupvm"].ToString() != string.Empty)
                    record.Attributes.Add("els_alkupaivamaara", DateTime.ParseExact(row["alkupvm"].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture));
                if (row["loppupvm"].ToString() != string.Empty)
                    record.Attributes.Add("els_paattymispaivamaara", DateTime.ParseExact(row["loppupvm"].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture));
                record.Attributes.Add("els_luottamustehtava", new OptionSetValue(GetTehtavaOptionSetValue(row["tehtava"].ToString())));
                if (row["paatoiminen"].ToString() == "E") {
                    record.Attributes.Add("els_paatoiminen", false); 
                }
                else
                {
                    record.Attributes.Add("els_paatoiminen", true);
                }
                record.Attributes.Add("els_sopimusalat", row["sopimusalat"].ToString());
                record.Attributes.Add("els_sektori", row["yrityksen_sektori"].ToString());
                record.Attributes.Add("els_juko_id", row["ID\r"].ToString().Replace("\r",""));
                record.Attributes.Add("els_soteorganisaatio", GetSoteOrganization("els_soteorganisaatiorekisteri", row["yrityksen_ytunnus"].ToString(), "els_ytunnus", serviceProvider.GetService()));

                #endregion

                if (record.Id == Guid.Empty)
                {
                    CreateRequest createRequest = new CreateRequest { Target = record };
                    exeReq.Requests.Add(createRequest);
                    insertsCounter++;
                }
                else
                {
                    UpdateRequest updateRequest = new UpdateRequest { Target = record };
                    exeReq.Requests.Add(updateRequest);
                    updatesCounter++;
                }
                batchLimit++;
            }
            crmLog.AddDetailInfo($"This session were inserted {insertsCounter} records and updated {updatesCounter}");
            return multipleReqList;
        }
        public static Entity GetLuottamusmiesRecord(string recKey, CRM_ServiceProvider serviceProvider)
        {
            Entity response = new Entity("els_luottamusmies");
            var existingRec = GetLuottamusmiesRecordFromCRM(recKey, serviceProvider.GetService());
            if (existingRec is null)
            {
                return response;
            }
            else
            {
                return existingRec;
            }
        }
        public static Dictionary<string, string> GetMappings()
        {
            Dictionary<string, string> mappings = new Dictionary<string, string>();
            mappings.Add("jasenjarjesto", "els_jasenjarjesto");//Item1 = CodeServerField ; Item2= CRMField
            mappings.Add("tehtava", "els_luottamustehtava");
            mappings.Add("alkupvm", "els_alkupaivamaara");
            mappings.Add("loppupvm", "els_paattymispaivamaara");
            mappings.Add("paatoiminen", "els_paatoiminen");
            mappings.Add("paahenkilo", "els_hetu");//???
            mappings.Add("varahenkilo", "els_hetu");
            mappings.Add("sopimusalat", "els_sopimusalat");
            mappings.Add("yrityksen_jukon_tunniste", "els_jukon_tunniste");
            // mappings.Add("yrityksen_nimi", "");Use  only for primary name field
            mappings.Add("yrityksen_ytunnus", "els_soteorganisaatio");//els_ytunnus 	Map businessid with Hierarchy lvl 0
            mappings.Add("yrityksen_sektori", "els_sektori");
            mappings.Add("ID", "els_juko_id");

            return mappings;
        }

        public static string SendToCRM(ExecuteMultipleRequest insertOrUpdateRequests, CRM_Logger crmLog, CRM_ServiceProvider serviceProvider)
        {
            string result = "OK";
            int CRMBatchSize = 500;
            List<ExecuteMultipleRequest> requestsList = new List<ExecuteMultipleRequest>();
            if (insertOrUpdateRequests.Requests.Any())
            {
                int len = insertOrUpdateRequests.Requests.Count % CRMBatchSize == 0 ? insertOrUpdateRequests.Requests.Count / CRMBatchSize : insertOrUpdateRequests.Requests.Count / CRMBatchSize + 1;
                for (int i = 0; i < len; i++)
                {
                    ExecuteMultipleRequest requests = new ExecuteMultipleRequest()
                    {
                        Settings = new ExecuteMultipleSettings()
                        {
                            ContinueOnError = true,
                            ReturnResponses = true
                        },
                        Requests = new OrganizationRequestCollection()
                    };

                    requests.Requests.AddRange(insertOrUpdateRequests.Requests.Skip(i * CRMBatchSize).Take(CRMBatchSize));
                    requestsList.Add(requests);
                }
            }
            insertOrUpdateRequests.Requests.Clear();

            if (requestsList.Count > 0)
            {
                foreach (ExecuteMultipleRequest req in requestsList)
                {
                    try
                    {
                        crmLog.AddDetailInfo($"Executing {req.Requests.Count} requests...");
                        var service = serviceProvider.GetService();
                        var response = (ExecuteMultipleResponse)service.Execute(req);
                        response.ResponseName = req.RequestName;

                        foreach (ExecuteMultipleResponseItem rsp in response.Responses.Where(r => r.Fault != null))
                        {
                            string msg = (rsp.Fault.Message ?? "") + " " + (rsp.Fault.TraceText ?? ""); 
                            crmLog.AddDetailInfo(msg);
                        }
                    }
                    catch (Exception reqEx)
                    {
                        crmLog.AddDetailInfo($"Error at ImportingToCRM \n {reqEx.Message}");
                        result = "Not OK";
                    }
                }

                requestsList.Clear();
                requestsList = null;
            }
            return result;
        }
        public static string ExecuteRequests(List<ExecuteMultipleRequest> reqList, CRM_ServiceProvider serviceProvider)
        {
            try
            {
                var service = serviceProvider.GetService();
                foreach (var request in reqList)
                {
                    service.Execute(request);
                }
                return "OK";
            }
            catch (Exception ex)
            {
                return "Error:" + ex.Message;
            }
        }
        public static Entity GetLuottamusmiesRecordFromCRM(string fieldValue, IOrganizationService service)
        {
            var query = new QueryExpression("els_luottamusmies");
            var query_contact = query.AddLink("contact", "els_henkilo", "contactid");

            query_contact.LinkCriteria.AddCondition("els_hetu", ConditionOperator.Equal, fieldValue);

            EntityCollection ecEntity = service.RetrieveMultiple(query);

            if (ecEntity.Entities.Count > 0)
            {
                return ecEntity.Entities.First();
            }
            return null;
        }
        public static EntityReference GetRecord(string entityName, string conditionFieldValue, string conditionField, IOrganizationService service)
        {
            QueryExpression qryEntity = new QueryExpression(entityName);
            qryEntity.Criteria.AddCondition(conditionField, ConditionOperator.Equal, conditionFieldValue);

            EntityCollection ecEntity = service.RetrieveMultiple(qryEntity);

            if (ecEntity.Entities.Count > 0)
            {
                return ecEntity.Entities[0].ToEntityReference();
            }
            return null;
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
        private static int GetTehtavaOptionSetValue(string tehtava)
        {
            int value = 861120000;
            switch (tehtava)
            {
                case "PLM":
                    value = (int)TEHTAVA.PLM;
                    break;
                case "VPLM":
                    value = (int)TEHTAVA.VPLM;
                    break;
                case "LM":
                    value = (int)TEHTAVA.LM;
                    break;
                case "VLM":
                    value = (int)TEHTAVA.VLM;
                    break;
                case "LV":
                    value = (int)TEHTAVA.LV;
                    break;
                default:
                    break;
            }
            return value;
        }

        private static EntityReference GetSoteOrganization(string entityName, string conditionFieldValue, string conditionField, IOrganizationService service)
        {

            QueryExpression qryEntity = new QueryExpression(entityName);
            qryEntity.Criteria.AddCondition(conditionField, ConditionOperator.Equal, conditionFieldValue);
            qryEntity.Criteria.AddCondition("els_hierarchylevel", ConditionOperator.Equal, 0);

            EntityCollection ecEntity = service.RetrieveMultiple(qryEntity);

            if (ecEntity.Entities.Count > 0)
            {
                return ecEntity.Entities[0].ToEntityReference();
            }
            return null;

        }

    }
}

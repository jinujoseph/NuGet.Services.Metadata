using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Ng.TraceListeners.Models;
using NuGet.Search.Common.ElasticSearch;
using NuGet.Search.Common.ElasticSearch.Sarif;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.IndexerService
{
    public class AzureStorageTableSarifProvider : ISarifProvider
    {
        public AzureStorageTableSarifProvider(string connectionString, string tableName, string elasticSearchEndpoint, string elasticSearchIndex)
        {
            this.ConnectionString = connectionString;
            this.TableName = tableName;
            this.ElasticSearchEndpoint = elasticSearchEndpoint;
            this.ElasticSearchIndex = elasticSearchIndex;
        }

        public string ConnectionString
        {
            get;
            private set;
        }

        public string TableName
        {
            get;
            private set;
        }

        public string ElasticSearchEndpoint
        {
            get;
            private set;
        }

        public string ElasticSearchIndex
        {
            get;
            private set;
        }

        public CloudTable Table
        {
            get
            {
                // Reconnect each time? I've no idea what the lifetime of these objects are...
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(this.ConnectionString);
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                CloudTable table = tableClient.GetTableReference(this.TableName);

                return table;
            }
        }

        public ElasticSearchClient ElasticSearchClient
        {
            get
            {
                ElasticSearchClient client = new ElasticSearchClient(this.ElasticSearchEndpoint, this.ElasticSearchIndex);

                return client;
            }
        }

        public IEnumerable<ResultLog> GetNextBatch()
        {
            List<ResultLog> newLogs = new List<ResultLog>();

            string lastRowKey;
            var response = this.ElasticSearchClient.Client.Search<ResultLog>(r => r.Take(1).Sort(s => s.Descending(f => f.RunLogs[0].Results.Last().Properties["RowKey"])));
            if (response.Hits.Count() == 0)
            {
                lastRowKey = "635910148343659110";
            }
            else
            {
                lastRowKey = response.Hits.First().Source.RunLogs[0].Results.Last().Properties["RowKey"];
            }

            TableQuery<Status> rangeQuery = new TableQuery<Status>().Where(
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThan, lastRowKey)).Take(500);

            IEnumerable<Status> newStatusLogs = this.Table.ExecuteQuery(rangeQuery);

            Trace.TraceInformation($"Received {newStatusLogs.Count()} from Azure Storage table.");

            var logGroups = newStatusLogs.GroupBy(row => row.GetKey());

            Trace.TraceInformation($"Grouped rows into {logGroups.Count()} runs.");

            foreach (var group in logGroups)
            {
                Status firstRow = group.OrderBy(row => row.RowKey).First();
                ResultLog log = new ResultLog(group.Key);
                log.RunLogs = new List<RunLog>();

                RunLog runLog = new RunLog();

                Status toolInfoRow = group.Where(row => row.Message.Equals("NG908")).FirstOrDefault();
                if (toolInfoRow != null)
                {
                    runLog.ToolInfo = JsonConvert.DeserializeObject<ToolInfo>(toolInfoRow.Data);
                }

                Status runInfoRow = group.Where(row => row.Message.Equals("NG909")).FirstOrDefault();
                if (runInfoRow != null)
                {
                    RunInfo runInfo = JsonConvert.DeserializeObject<RunInfo>(runInfoRow.Data);
                    runInfo.Machine = runInfoRow.Machine;
                    runInfo.RunDate = runInfoRow.EventTime;
                    runInfo.ProcessId = runInfoRow.ProcessId;
                    runLog.RunInfo = runInfo;
                }

                runLog.Results = new List<Result>();
                foreach (Status row in group)
                {
                    if (!row.Message.Equals("NG908") && !row.Message.Equals("NG909"))
                    {
                        Result result = JsonConvert.DeserializeObject<Result>(row.Data);
                        result.Properties["PartitionKey"] = row.PartitionKey;
                        result.Properties["RowKey"] = row.RowKey;
                        runLog.Results.Add(result);
                    }
                }

                log.RunLogs.Add(runLog);

                newLogs.Add(log);
            }

            return newLogs;
        }
    }
}

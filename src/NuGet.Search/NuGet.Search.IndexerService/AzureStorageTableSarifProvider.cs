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
    /// <summary>
    /// Retrieves logs from azure table storage.
    /// </summary>
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

            // Find the last log that was indexed. We'll use this to only retrieve Azure logs that were created
            // after this date.
            string lastRowKey;
            var response = this.ElasticSearchClient.Client.Search<ResultLog>(r => r.Take(1).Sort(s => s.Descending(f => f.RunLogs[0].Results.Last().Properties["RowKey"])));
            if (response.Hits.Count() == 0)
            {
                // This is a day in early Feb 2016.
                lastRowKey = "635910148343659110";
            }
            else
            {
                // This is the last log that was indexed.
                lastRowKey = response.Hits.First().Source.RunLogs[0].Results.Last().Properties["RowKey"];
            }

            // Get the next 500 logs.
            TableQuery<Status> rangeQuery = new TableQuery<Status>().Where(
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThan, lastRowKey)).Take(500);

            IEnumerable<Status> newStatusLogs = this.Table.ExecuteQuery(rangeQuery);

            Trace.TraceInformation($"Received {newStatusLogs.Count()} from Azure Storage table.");

            // Group the logs by tool run. The key is a combination of date, machine, application and process id.
            // This key should group logs by a tool run.
            var logGroups = newStatusLogs.GroupBy(row => row.GetKey());

            Trace.TraceInformation($"Grouped rows into {logGroups.Count()} runs.");

            foreach (var group in logGroups)
            {
                Status firstRow = group.OrderBy(row => row.RowKey).First();
                ResultLog log = new ResultLog(group.Key);
                log.RunLogs = new List<RunLog>();

                RunLog runLog = new RunLog();

                // NG908 signifies that the data field contains a serialized ToolInfo.
                Status toolInfoRow = group.Where(row => row.Message.Equals("NG908")).FirstOrDefault();
                if (toolInfoRow != null)
                {
                    runLog.ToolInfo = JsonConvert.DeserializeObject<ToolInfo>(toolInfoRow.Data);
                }

                // NG909 signifies that the data field contains a serialized RunInfo.
                Status runInfoRow = group.Where(row => row.Message.Equals("NG909")).FirstOrDefault();
                if (runInfoRow != null)
                {
                    RunInfo runInfo = JsonConvert.DeserializeObject<RunInfo>(runInfoRow.Data);

                    // Add a few extra properties to the run info. These are to be displayed on the web site.
                    runInfo.Machine = runInfoRow.Machine;
                    runInfo.RunDate = runInfoRow.EventTime;
                    runInfo.ProcessId = runInfoRow.ProcessId;
                    runLog.RunInfo = runInfo;
                }

                // Every other row is a seralized Result object.
                runLog.Results = new List<Result>();
                foreach (Status row in group)
                {
                    if (!row.Message.Equals("NG908") && !row.Message.Equals("NG909"))
                    {
                        Result result = JsonConvert.DeserializeObject<Result>(row.Data);

                        // Add a few extra properties which identify the Azure row the result came from.
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

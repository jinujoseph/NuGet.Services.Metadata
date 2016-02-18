using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
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
                runLog.ToolInfo = new ToolInfo();
                runLog.ToolInfo.FullName = firstRow.Application.Trim();
                runLog.ToolInfo.Name = Path.GetFileNameWithoutExtension(firstRow.Application.Trim());
                runLog.ToolInfo.FileVersion = "0.0.0.0";
                runLog.ToolInfo.Version = "0.0.0.0";

                runLog.RunInfo = new RunInfo();
                runLog.RunInfo.InvocationInfo = firstRow.RowKey.Trim();
                runLog.RunInfo.Machine = firstRow.Machine.Trim();
                runLog.RunInfo.ProcessId = firstRow.ProcessId;
                runLog.RunInfo.RunDate = firstRow.EventTime;

                runLog.Results = new List<Result>();
                foreach (Status row in group)
                {
                    Result result = new Result();

                    if (String.IsNullOrWhiteSpace(row.State) && String.IsNullOrWhiteSpace(row.Result) && !String.IsNullOrWhiteSpace(row.Details))
                    {
                        result.RuleId = "NGI001";
                        result.ShortMessage = $"{row.Details.Trim()}";
                        result.FullMessage = result.ShortMessage;
                    }
                    else if (row.Result.Trim().Equals("Start"))
                    {
                        result.RuleId = "NGI002";
                        result.ShortMessage = $"Start activity {row.Activity.Trim()}";
                        result.FullMessage = result.ShortMessage;
                    }
                    else if (row.Result.Trim().Equals("Stop"))
                    {
                        result.RuleId = "NGI003";
                        result.ShortMessage = $"Stop activity {row.Activity.Trim()}, {row.Details.Trim()}";
                        result.FullMessage = result.ShortMessage;
                    }
                    else if (row.Result.Trim().Equals("Options"))
                    {
                        result.RuleId = "NGI004";
                        result.ShortMessage = $"Appliation arguments" + Environment.NewLine + row.Details.Trim();
                        result.FullMessage = result.ShortMessage;
                    }
                    else if (row.Result.Trim().Equals("NoLatestStable"))
                    {
                        result.RuleId = "NGI005";
                        result.ShortMessage = $"No latest stable version for package {row.State.Trim()}.";
                        result.FullMessage = result.ShortMessage;
                    }
                    else if (row.Result.Trim().Equals("IdxNotCreated"))
                    {
                        result.RuleId = "NGI006";
                        result.ShortMessage = $"Idx file not created for package {row.State.Trim()}.";
                        result.FullMessage = result.ShortMessage;
                    }
                    else if (row.Result.Trim().Equals("DecompressFail"))
                    {
                        result.RuleId = "NGI007";
                        result.ShortMessage = $"Could not decompress package {row.State.Trim()}.";
                        result.FullMessage = result.ShortMessage + " " + row.Details.Trim();
                    }

                    result.Kind = row.Level;

                    result.Properties = new Dictionary<string, string>();
                    result.Properties.Add("PartitionKey", row.PartitionKey.Trim());
                    result.Properties.Add("RowKey", row.RowKey.Trim());
                    result.Properties.Add("Machine", row.Machine.Trim());
                    result.Properties.Add("ThreadId", row.ThreadId.ToString());
                    result.Properties.Add("Activity", row.Activity.Trim());
                    result.Properties.Add("EventTime", row.EventTime.ToUniversalTime().ToString());
                    result.Properties.Add("State", row.State.Trim());
                    result.Properties.Add("Result", row.Result.Trim());
                    result.Properties.Add("Details", row.Details.Trim());
                    result.Properties.Add("Application", row.Application.Trim());
                    result.Properties.Add("Level", row.Level.Trim());
                    result.Properties.Add("ProcessId", row.ProcessId.ToString());

                    runLog.Results.Add(result);
                }

                log.RunLogs.Add(runLog);

                newLogs.Add(log);
            }

            return newLogs;
        }
    }
}

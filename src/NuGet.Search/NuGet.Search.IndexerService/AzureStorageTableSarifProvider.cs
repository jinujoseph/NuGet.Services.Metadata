using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Ng.TraceListeners.Models;
using NuGet.Search.Common.ElasticSearch;
using NuGet.Search.Common.ElasticSearch.Sarif;
using System;
using System.Collections.Generic;
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

            TableQuery<Status> rangeQuery = new TableQuery<Status>().Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "635908320000000000"),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThan, "635909146658108307")));

            IEnumerable<Status> newStatusLogs = this.Table.ExecuteQuery(rangeQuery);

            var logGroups = newStatusLogs.GroupBy(row => row.GetKey());

            foreach (var group in logGroups)
            {
                Status firstRow = group.OrderBy(row => row.RowKey).First();
                ResultLog log = new ResultLog(group.Key);

                RunLog runLog = new RunLog();
                runLog.ToolInfo = new ToolInfo();
                runLog.ToolInfo.FullName = firstRow.Application;
                runLog.ToolInfo.Name = Path.GetFileNameWithoutExtension(firstRow.Application);
                runLog.ToolInfo.FileVersion = "0.0.0.0";
                runLog.ToolInfo.Version = "0.0.0.0";

                runLog.Results = new List<Result>();
                foreach (Status row in group)
                {
                    Result result = new Result();

                    if (String.IsNullOrWhiteSpace(row.State) && String.IsNullOrWhiteSpace(row.Result) && !String.IsNullOrWhiteSpace(row.Details))
                    {
                        result.RuleId = "NGI001";
                        result.FullMessage = $"{row.Details}";
                    }
                    else if (row.Result.Equals("Start"))
                    {
                        result.RuleId = "NGI002";
                        result.FullMessage = $"Start activity {row.Activity}";
                    }
                    else if (row.Result.Equals("Stop"))
                    {
                        result.RuleId = "NGI003";
                        result.FullMessage = $"Stop activity {row.Activity}, {row.Details}";
                    }
                    else if (row.Result.Equals("Options"))
                    {
                        result.RuleId = "NGI004";
                        result.FullMessage = $"Appliation arguments" + Environment.NewLine + row.Details;
                    }
                    else if (row.Result.Equals("NoLatestStable"))
                    {
                        result.RuleId = "NGI005";
                        result.FullMessage = $"No latest stable version for package {row.State}.";
                    }
                    else if (row.Result.Equals("IdxNotCreated"))
                    {
                        result.RuleId = "NGI006";
                        result.FullMessage = $"Idx file not created for package {row.State}.";
                    }
                    else if (row.Result.Equals("DecompressFail"))
                    {
                        result.RuleId = "NGI007";
                        result.FullMessage = $"Could not decompress package {row.State}.";
                    }

                    result.Kind = row.Level;


                    result.Properties = new Dictionary<string, string>();
                    result.Properties.Add("PartitionKey", row.PartitionKey);
                    result.Properties.Add("RowKey", row.RowKey);
                    result.Properties.Add("Machine", row.Machine);
                    result.Properties.Add("ThreadId", row.ThreadId.ToString());
                    result.Properties.Add("Activity", row.Activity);
                    result.Properties.Add("EventTime", row.EventTime.ToUniversalTime().ToString());
                    result.Properties.Add("State", row.State);
                    result.Properties.Add("Result", row.Result);
                    result.Properties.Add("Details", row.Details);
                    result.Properties.Add("Application", row.Application);
                    result.Properties.Add("Level", row.Level);
                    result.Properties.Add("ProcessId", row.ProcessId.ToString());
                }
            }

            return newLogs;
        }
    }
}

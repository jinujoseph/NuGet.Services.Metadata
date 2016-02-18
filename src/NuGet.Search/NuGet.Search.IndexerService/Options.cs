using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.IndexerService
{
    public class Options
    {
        public Options()
        {
        }

        private const string elasticSearchText = "elasticsearch";
        private const string indexText = "index";
        private const string maxThreadsText = "maxthreads";
        private const string azureConnectionStringText = "azureconnectionstring";
        private const string azureTableNameText = "azuretablename";

        [Option('e', Options.elasticSearchText, Required = false, HelpText = @"The URL of the ElasticSearch server.")]
        public string ElasticSearchServerUrl { get; set; }

        [Option('i', Options.indexText, Required = false, HelpText = @"The name of the index.")]
        public string IndexName { get; set; }

        [Option('t', Options.maxThreadsText, Required = false, HelpText = @"The maximum number of threads to for in indexing.")]
        public int MaxIndexerThreads { get; set; }

        [Option('a', Options.azureConnectionStringText, Required = false, HelpText = @"The connection string for the Azure Storage account which contain the logs to index.")]
        public string AzureStorageConnectionString { get; set; }

        [Option('t', Options.azureTableNameText, Required = false, HelpText = @"The Azure Storage table name which contains the logs to index.")]
        public string AzureStorageTableName { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        public string GetParameterValueText()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Input Parameters" + Environment.NewLine);
            text.Append($"    {elasticSearchText}: {this.ElasticSearchServerUrl}{ Environment.NewLine}");
            text.Append($"    {indexText}: {this.IndexName}{ Environment.NewLine}");
            text.Append($"    {maxThreadsText}: {this.MaxIndexerThreads}{ Environment.NewLine}");
            text.Append($"    {azureConnectionStringText}: {this.AzureStorageConnectionString}{ Environment.NewLine}");
            text.Append($"    {azureTableNameText}: {this.AzureStorageTableName}{ Environment.NewLine}");

            return text.ToString();
        }

        public void ReadAppConfigValues()
        {
            this.ElasticSearchServerUrl = GetConfigValue(elasticSearchText, String.Empty);
            this.IndexName = GetConfigValue(indexText, String.Empty);
            this.MaxIndexerThreads = GetConfigValue(maxThreadsText, 1);
            this.AzureStorageConnectionString = GetConfigValue(azureConnectionStringText, String.Empty);
            this.AzureStorageTableName = GetConfigValue(azureTableNameText, String.Empty);
        }

        public bool Validate()
        {
            List<string> validationErrors = new List<string>();

            if (String.IsNullOrWhiteSpace(this.ElasticSearchServerUrl))
            {
                validationErrors.Add("elasticsearch must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.IndexName))
            {
                validationErrors.Add("index must be specified.");
            }

            if (this.MaxIndexerThreads < 1)
            {
                validationErrors.Add("maxthreads must be greater than zero.");
            }

            if (String.IsNullOrWhiteSpace(this.AzureStorageConnectionString))
            {
                validationErrors.Add("azureconnectionstring must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.AzureStorageTableName))
            {
                validationErrors.Add("azuretablename must be specified.");
            }

            if (validationErrors.Count > 0)
            {
                Trace.TraceError(String.Join(Environment.NewLine, validationErrors.ToArray()));
                return false;
            }
            else
            {
                return true;
            }
        }

        private String GetConfigValue(String keyName, String defaultValue)
        {
            String value = System.Configuration.ConfigurationManager.AppSettings[keyName];

            if (value == null)
            {
                value = defaultValue;
            }

            return value;
        }

        private int GetConfigValue(String keyName, int defaultValue)
        {
            int value;
            String valueText = System.Configuration.ConfigurationManager.AppSettings[keyName];

            if (String.IsNullOrWhiteSpace(valueText) || !Int32.TryParse(valueText, out value))
            {
                value = defaultValue;
            }

            return value;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using Ng.TraceListeners.Models;

namespace Ng.TraceListeners
{
    /// <summary>
    /// Saves application status data to Azure table storage.
    /// Only messages in the following form are written to table storage.
    ///   #Status #Activity=Download packages #Status=Begin #Details=Download source http://foo/downloads
    /// Register the trace listener using the following. Specify the Azure table storage connection
    /// string using the tablestorage field. Note that this trace listener uses * as the field delimiter
    /// because the = character is used in the connection string.
    /// <add name="AzureTableStatus" type="Ng.TraceListeners.AzureTableStorageStatusTraceListener, Ng.TraceListeners" initializeData="tablestorage*connectionstring" />
    /// </summary>
    public class AzureTableStorageStatusTraceListener : TraceListenerBase
    {
        CloudStorageAccount _storageAccount = null;
        CloudTableClient _tableClient = null;
        CloudTable _table = null;

        public AzureTableStorageStatusTraceListener(String initializeData) : base(initializeData)
        {
            string connectionString = null;
            string tableName = "status";

            if (initializeData != null)
            {
                foreach (String keyValuePair in initializeData.Split(','))
                {
                    String[] parts = keyValuePair.Split('*');
                    if (parts.Length == 2)
                    {
                        if (parts[0].Equals("tablestorage", StringComparison.InvariantCultureIgnoreCase))
                        {
                            connectionString = parts[1].Trim();
                        }
                        else if (parts[0].Equals("table", StringComparison.InvariantCultureIgnoreCase))
                        {
                            tableName = parts[1].Trim();
                        }
                    }
                }
            }

            if (String.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException("tablestorage", "The initializeData string must specify the Azure table storage connection string in the tablestorage field.");
            }

            this._storageAccount = CloudStorageAccount.Parse(connectionString);
            this._tableClient = this._storageAccount.CreateCloudTableClient();
            this._table = this._tableClient.GetTableReference(tableName);
            this._table.CreateIfNotExists();
        }

        protected override void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message)
        {
            // Called by Trace.WriteLne(). Ignore.
        }

        protected override void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message, Int32 eventId, TraceEventCache eventCache, String source)
        {
            if (_table != null)
            {
                if (message.StartsWith("#Status ") || message.StartsWith("#Status:"))
                {
                    Status status = new Status();
                    status.Level = eventType.ToString();
                    status.ThreadId = threadId;

                    string trimmedMessage = message.Substring(8);

                    string[] parts = trimmedMessage.Split(new[] { '#' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string part in parts)
                    {
                        int propertyNameIndex = part.IndexOf('=');

                        if (propertyNameIndex > 0)
                        {
                            string propertyName = part.Substring(0, propertyNameIndex);
                            string propertyValue = part.Substring(propertyNameIndex + 1);

                            switch (propertyName.ToLowerInvariant())
                            {
                                case "activity":
                                    status.Activity = propertyValue;
                                    break;
                                case "state":
                                    status.State = propertyValue;
                                    break;
                                case "result":
                                    status.Result = propertyValue;
                                    break;
                                case "details":
                                    if (!string.IsNullOrWhiteSpace(status.Details))
                                    {
                                        status.Details += "; ";
                                    }

                                    status.Details += propertyValue;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unknown trace property name: {propertyName}");
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(status.Details))
                            {
                                status.Details += "; ";
                            }

                            status.Details += part;
                        }
                    }

                    TableOperation operation = TableOperation.Insert(status);
                    this._table.Execute(operation);
                }
            }
        }
    }
}

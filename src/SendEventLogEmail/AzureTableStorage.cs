using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Ng.TraceListeners.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.SendEventLogEmail
{
    public class AzureTableStorage
    {
        CloudStorageAccount _storageAccount = null;
        CloudTableClient _tableClient = null;
        CloudTable _table = null;

        public AzureTableStorage(string connectionString, string tableName)
        {
            this._storageAccount = CloudStorageAccount.Parse(connectionString);
            this._tableClient = this._storageAccount.CreateCloudTableClient();
            this._table = this._tableClient.GetTableReference(tableName);
            this._table.CreateIfNotExists();
        }

        public bool IsKnownLog(string level, string message)
        {
            string messageLine = message.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).First().Trim();

            TableOperation retrieveOperation = TableOperation.Retrieve<KnownLog>(level, messageLine);
            TableResult retrievedResult = this._table.Execute(retrieveOperation);

            return retrievedResult != null;
        }
    }
}

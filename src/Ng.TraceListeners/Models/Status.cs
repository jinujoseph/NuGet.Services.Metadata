using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng.TraceListeners.Models
{
    public class Status : TableEntity
    {
        /// <summary>
        /// Do not use. Available for serialization purposes only.
        /// </summary>
        public Status()
        {
        }

        public Status(DateTime eventTime)
        {
            this.Machine = Environment.MachineName;
            this.ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            this.EventTime = eventTime.ToUniversalTime();
            this.Application = System.Reflection.Assembly.GetEntryAssembly() != null ? Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location) : null;
            this.ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

            this.Message = String.Empty;
            this.Data = String.Empty;
            this.Level = String.Empty;

            DateTime creationTime = DateTime.Now;
            this.PartitionKey = creationTime.Date.Ticks.ToString();
            this.Ticks = creationTime.Ticks;
            this.RowKey = Guid.NewGuid().ToString();
        }

        public string Machine { get; set; }
        public int ThreadId { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
        public DateTime EventTime { get; set; }
        public long Ticks { get; set; }

        public string Application { get; set; }
        public int ProcessId { get; set; }
        public string Level { get; set; }
    }
}

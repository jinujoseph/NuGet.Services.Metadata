using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.TraceListeners.Models
{
    public class Status : TableEntity
    {
        DateTime _recordedOn;

        public Status() : this(DateTime.Now)
        {
        }

        public Status(DateTime eventTime)
        {
            this.Machine = Environment.MachineName;
            this.ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            this.EventTime = eventTime.ToUniversalTime();
            this.Application = System.Reflection.Assembly.GetEntryAssembly() != null ? Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location) : null;

            this.Activity = String.Empty;
            this.State = String.Empty;
            this.Result = String.Empty;
            this.Details = String.Empty;
            this.Level = String.Empty;
        }

        public string Machine { get; set; }

        public int ThreadId { get; set; }
        public string Activity { get; set; }
        public DateTime EventTime
        {
            get
            {
                return this._recordedOn;
            }
            set
            {
                this._recordedOn = value;
                this.PartitionKey = value.Date.Ticks.ToString();
                this.RowKey = value.Ticks.ToString();
            }
        }

        public string State { get; set; }
        public string Result { get; set; }
        public string Details { get; set; }

        public string Application { get; set; }
        public string Level { get; set; }
    }
}

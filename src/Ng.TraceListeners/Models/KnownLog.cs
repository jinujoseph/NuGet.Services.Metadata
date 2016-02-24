using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.TraceListeners.Models
{
    public class KnownLog : TableEntity
    {
        private string _level;
        private string _message;

        public DateTime CreatedOn { get; set; }

        public string Level
        {
            get
            {
                return this._level;
            }
            set
            {
                this._level = value;
                this.PartitionKey = value;
            }
        }

        public string Message
        {
            get
            {
                return this._message;
            }
            set
            {
                this._message = value;
                this.RowKey = value;
            }
        }
    }
}

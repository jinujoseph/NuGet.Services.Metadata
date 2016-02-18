using Ng.TraceListeners.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.IndexerService
{
    public static class StatusExtensions
    {
        public static string GetKey(this Status status)
        {
            return $"{status.PartitionKey}_{status.Machine}_{status.Application}_{status.ProcessId}";
        }
    }
}

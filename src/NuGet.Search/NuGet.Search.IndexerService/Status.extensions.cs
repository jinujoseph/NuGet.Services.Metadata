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
            // The key is passed via url, so it can't contain any invalid chars
            return $"{status.PartitionKey}_{status.Machine}_{status.Application}_{status.ProcessId}".Replace('.','-').Replace('/', '-').Replace('\\', '-');
        }
    }
}

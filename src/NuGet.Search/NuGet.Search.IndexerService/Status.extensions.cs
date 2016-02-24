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
        /// <summary>
        /// Generates a unique key/string for the tool run.
        /// The key is comprised of the date, machine, application and process id.
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        public static string GetKey(this Status status)
        {
            // The key is passed via url, so it can't contain any invalid chars
            return $"{status.PartitionKey}_{status.Machine}_{status.Application}_{status.ProcessId}".Replace('.','-').Replace('/', '-').Replace('\\', '-');
        }
    }
}


using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Sdk;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Sarif.Sdk
{
    public static class RunInfoExtensions
    {
        /// <summary>
        /// Serializes a sarif RunInfo to json.
        /// </summary>
        public static string ToJson(this RunInfo runInfo)
        {
            string json = JsonConvert.SerializeObject(runInfo, Formatting.None);

            return json;
        }
    }
}

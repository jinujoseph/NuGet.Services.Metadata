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
    public static class ToolInfoExtensions
    {
        /// <summary>
        /// Serializes a sarif ToolInfo to json.
        /// </summary>
        public static string ToJson(this ToolInfo toolInfo)
        {
            string json = JsonConvert.SerializeObject(toolInfo, Formatting.None);

            return json;
        }
    }
}

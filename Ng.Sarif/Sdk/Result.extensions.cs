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
    public static class ResultExtensions
    {
        /// <summary>
        /// Serializes a sarif Result to json.
        /// </summary>
        public static string ToJson(this Result result)
        {
            string json = JsonConvert.SerializeObject(result, Formatting.None);

            return json;
        }
    }
}

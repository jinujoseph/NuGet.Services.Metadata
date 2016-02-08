using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Models
{
    public class PackageInfo
    {
        [JsonProperty("id")]
        public string PackageId
        {
            get;
            set;
        }

        [JsonProperty("latestStableVersion")]
        public string LatestStableVersion
        {
            get;
            set;
        }

        [JsonProperty("commitId")]
        public Guid CommitId
        {
            get;
            set;
        }
    }
}


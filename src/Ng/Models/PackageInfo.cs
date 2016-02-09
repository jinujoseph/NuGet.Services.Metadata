// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ng.Models
{
    /// <summary>
    /// Model for the resources nodes of http://api.nuget.org/v3/index.json#resources.
    /// </summary>
    internal class ServiceIndexResource
    {
        [JsonProperty(PropertyName = "@id")]
        public Uri Id
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "@type")]
        public string Type
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "comment")]
        public string Comment
        {
            get;
            private set;
        }
    }
}


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
    /// Model for the NuGet service index, http://api.nuget.org/v3/index.json.
    /// </summary>
    internal class ServiceIndex
    {
        [JsonProperty(PropertyName = "version")]
        public string Version
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "resources")]
        public ServiceIndexResource[] Resources
        {
            get;
            private set;
        }

        public bool TryGetResourceId(string type, out Uri id)
        {
            id = this.Resources.FirstOrDefault(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase))?.Id;
            return id != null;
        }

        public static ServiceIndex Deserialize(Uri serviceIndex)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString(serviceIndex);
                return ServiceIndex.Deserialize(json);
            }
        }

        public static ServiceIndex Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<ServiceIndex>(json);
        }
    }
}


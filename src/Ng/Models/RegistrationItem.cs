// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace Ng.Models
{
    /// <summary>
    /// Model for a NuGet registration item, https://api.nuget.org/v3/registration1/newtonsoft.json/8.0.2.json.
    /// </summary>
    public class RegistrationItem
    {
        [JsonProperty(PropertyName = "@id")]
        public Uri Id
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "@type")]
        public string[] Type
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "catalogEntry")]
        public Uri CatalogEntry
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "listed")]
        public bool Listed
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "packageContent")]
        public Uri PackageContent
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "published")]
        public DateTime Published
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "registration")]
        public Uri Registration
        {
            get;
            private set;
        }

        public static RegistrationItem Deserialize(Uri registrationItemUrl)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString(registrationItemUrl);
                return RegistrationItem.Deserialize(json);
            }
        }

        public static RegistrationItem Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<RegistrationItem>(json);
        }
    }
}


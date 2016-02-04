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
    /// Model for the dependency node for, https://api.nuget.org/v3/catalog0/data/2015.02.02.16.48.21/angularjs.1.0.2.json#dependencygroup.
    /// </summary>
    public class Dependency
    {
        [JsonProperty(PropertyName = "@id")]
        public Uri Id
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "@type")]
        public String Type
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "id")]
        public string DependencyId
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "range")]
        public string VersionRange
        {
            get;
            private set;
        }
    }
}


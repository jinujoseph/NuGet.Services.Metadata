// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ng.Models
{
    /// <summary>
    /// Model package registration information contained in the registartion index, 
    /// https://api.nuget.org/v3/registration1/newtonsoft.json/index.json#items#items#items.
    /// </summary>
    /// <remarks>The registration index contains all the versions of a package. This type 
    /// contains the registration information for one version of the package.</remarks>
    public abstract class RegistrationPackage
    {
        [JsonProperty(PropertyName = "@id")]
        public Uri Id
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "@type")]
        public string Type
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "catalogEntry")]
        public RegistrationIndexPackageDetails CatalogEntry
        {
            get;
            set;
        }
    }
}
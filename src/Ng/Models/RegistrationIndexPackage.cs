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
    public class RegistrationIndexPackage
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

        [JsonProperty(PropertyName = "commitId")]
        public Guid CommitId
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "commitTimeStamp")]
        public DateTime CommitTimeStamp
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "packageContent")]
        public Uri PackageContent
        {
            get;
            internal set;
        }

        [JsonProperty(PropertyName = "registration")]
        public Uri Registration
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "catalogEntry")]
        public RegistrationIndexPackageDetails CatalogEntry
        {
            get;
            set;
        }

        /// <summary>
        /// Indicates if the package is a NuGet package or a local (fake) package.
        /// </summary>
        [JsonIgnore]
        public bool IsLocalPackage
        {
            get
            {
                return this.Id.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
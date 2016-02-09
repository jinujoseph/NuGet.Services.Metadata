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
    /// Model package catalog information contained in the registartion index, 
    /// https://api.nuget.org/v3/registration1/newtonsoft.json/index.json#items#items#catalogEntry.
    /// </summary>
    /// <remarks>The registration index contains all the versions of a package. This type 
    /// contains the package details for one version of the package.</remarks>
    public class RegistrationIndexPackageDetails
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

        [JsonProperty(PropertyName = "authors")]
        public string Authors
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "description")]
        public string Description
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "iconUrl")]
        public Uri IconUrl
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "id")]
        public string PackageId
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "language")]
        public string Language
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "licenseUrl")]
        public Uri LicenseUrl
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

        [JsonProperty(PropertyName = "minClientVersion")]
        public string MinClientVersion
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

        [JsonProperty(PropertyName = "projectUrl")]
        public Uri ProjectUrl
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

        [JsonProperty(PropertyName = "requireLicenseAcceptance")]
        public bool? RequireLicenseAcceptance
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "summary")]
        public string Summary
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "tags")]
        public string[] Tags
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "title")]
        public string Title
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "version")]
        public string PackageVersion
        {
            get;
            private set;
        }
    }
}
﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Catalog = NuGet.Services.Metadata.Catalog;

namespace Ng.Models
{
    /// <summary>
    /// Model for a NuGet catalog item, https://api.nuget.org/v3/catalog0/data/2015.02.02.16.48.21/angularjs.1.0.2.json.
    /// </summary>
    public class CatalogItem
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

        [JsonProperty(PropertyName = "authors")]
        public string Authors
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "catalog:commitId")]
        public Guid CommitId
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "catalog:commitTimeStamp")]
        public DateTime CommitTimeStamp
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "copyright")]
        public string Copyright
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "created")]
        public DateTime Created
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
        public string IconUrl
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

        [JsonProperty(PropertyName = "isPrerelease")]
        public bool IsPrerelease
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

        [JsonProperty(PropertyName = "lastEdited")]
        public DateTime LastEdited
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "packageHash")]
        public string PackageHash
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "licenseNames")]
        public string LicenseNames
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "licenseReportUrl")]
        public Uri LicenseReportUrl
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

        [JsonProperty(PropertyName = "originalId")]
        public string OriginalId
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "packageHashAlgorithm")]
        public string PackageHashAlgorithm
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "packageSize")]
        public int PackageSize
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
        public DateTime? Published
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "releaseNotes")]
        public string ReleaseNotes
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "requireLicenseAcceptance")]
        public bool RequireLicenseAcceptance
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

        [JsonProperty(PropertyName = "verbatimVersion")]
        public string VerbatimVersion
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

        [JsonProperty(PropertyName = "dependencyGroups")]
        public DependencyGroup[] DependencyGroups
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

        [JsonIgnore]
        public bool IsPackageDetails
        {
            get
            {
                return this.Type.Contains("PackageDetails");
            }
        }

        [JsonIgnore]
        public bool IsPackageDelete
        {
            get
            {
                return this.Type.Contains("PackageDelete");
            }
        }

        public void NormalizeId()
        {
            if (!String.IsNullOrWhiteSpace(this.OriginalId))
            {
                this.PackageId = this.OriginalId;
            }
        }

        /// <summary>
        /// Gets the registration information for this item.
        /// The registration contains links to the download URL as-well-as basic publishing information.
        /// </summary>
        /// <param name="nugetServiceUrls">The NugetServiceUrl object which contains the NuGet service endpoints.</param>
        /// <returns></returns>
        internal RegistrationItem GetRegistrationItem(NugetServiceEndpoints nugetServiceUrls)
        {
            using (Catalog.CollectorHttpClient client = new Catalog.CollectorHttpClient())
            {
                // Download the registration json file
                Uri registrationUrl = nugetServiceUrls.ComposeRegistrationUrl(this.PackageId, this.PackageVersion);
                string registrationItemJson = client.GetStringAsync(registrationUrl).Result;
                RegistrationItem registrationItem = RegistrationItem.Deserialize(registrationItemJson);

                return registrationItem;
            }
        }

        public static CatalogItem Deserialize(Uri packageDetails)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString(packageDetails);
                return CatalogItem.Deserialize(json);
            }
        }

        public static CatalogItem Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<CatalogItem>(json);
        }
    }
}


using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Models
{
    /// <summary>
    /// Model for a NuGet registration item, https://api.nuget.org/v3/registration1/newtonsoft.json/8.0.2.json#items#items#catalogEntry.
    /// </summary>
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

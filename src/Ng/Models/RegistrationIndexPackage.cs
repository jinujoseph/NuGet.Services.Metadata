using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Models
{
    /// <summary>
    /// Model for a NuGet registration item, https://api.nuget.org/v3/registration1/newtonsoft.json/8.0.2.json#items#items.
    /// </summary>
    public class RegistrationIndexPackage
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
            private set;
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
            private set;
        }
    }
}

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
    /// Model for a page in the NuGet registration index, https://api.nuget.org/v3/registration1/newtonsoft.json/index.json#items.
    /// </summary>
    /// <remarks>The registration index contains all the versions of a package. If there are many
    /// versions, they might be grouped into pages of versions. This type represents one page of versions.</remarks>
    public class RegistrationIndexPageItem
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

        [JsonProperty(PropertyName = "count")]
        public int Count
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "parent")]
        public Uri Parent
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "lower")]
        public string Lower
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "upper")]
        public string Upper
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "items")]
        public RegistrationIndexPackage[] Items
        {
            get;
            private set;
        }

        public RegistrationIndexPageItem LoadPage()
        {
            return RegistrationIndexPageItem.Deserialize(this.Id);
        }

        /// <summary>
        /// Creates a RegistrationIndexPageItem object from the contents of a URL.
        /// </summary>
        /// <param name="registrationIndexUrl">The URL that returns the registration index page json.</param>
        /// <returns>A RegistrationIndexPageItem which represents the contents return by the URL.</returns>
        public static RegistrationIndexPageItem Deserialize(Uri registrationIndexUrl)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString(registrationIndexUrl);
                return RegistrationIndexPageItem.Deserialize(json);
            }
        }

        /// <summary>
        /// Creates a RegistrationIndexPageItem object from the contents of a json string.
        /// </summary>
        /// <param name="json">The json string that defines the registration index page.</param>
        /// <returns>A RegistrationIndexPageItem which represents the json string.</returns>
        public static RegistrationIndexPageItem Deserialize(string json)
        {
            RegistrationIndexPageItem item = JsonConvert.DeserializeObject<RegistrationIndexPageItem>(json);

            // Do some basic validation
            if (item == null)
            {
                throw new ArgumentOutOfRangeException("The json string was not a registration index page.");
            }

            if (item.Items == null)
            {
                throw new ArgumentOutOfRangeException("The json string did not have a value for the field items.");
            }

            return item;
        }
    }
}
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
    /// Model for a NuGet registration index, https://api.nuget.org/v3/registration1/newtonsoft.json/index.json.
    /// </summary>
    /// <remarks>The registration index is the list of all the versions for a package.</remarks>
    public class RegistrationIndex
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

        [JsonProperty(PropertyName = "items")]
        public RegistrationIndexPageItem[] Items
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a RegistrationIndex object from the contents of a URL.
        /// </summary>
        /// <param name="registrationIndexUrl">The URL that returns the registration index json.</param>
        /// <returns>A RegistrationIndex which represents the contents return by the URL.</returns>
        public static RegistrationIndex Deserialize(Uri registrationIndexUrl)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString(registrationIndexUrl);
                return RegistrationIndex.Deserialize(json);
            }
        }

        /// <summary>
        /// Creates a RegistrationIndex object from the contents of a json string.
        /// </summary>
        /// <param name="json">The json string that defines the registration index.</param>
        /// <returns>A RegistrationIndex which represents the json string.</returns>
        public static RegistrationIndex Deserialize(string json)
        {
            RegistrationIndex item = JsonConvert.DeserializeObject<RegistrationIndex>(json);

            // Do some basic validation
            if (item == null)
            {
                throw new ArgumentOutOfRangeException("The json string was not a registration index.");
            }

            if (item.Items == null)
            {
                throw new ArgumentOutOfRangeException("The json string did not have a value for the field items.");
            }

            return item;
        }
    }
}
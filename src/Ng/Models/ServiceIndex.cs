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
    /// Model for the NuGet service index, http://api.nuget.org/v3/index.json.
    /// </summary>
    internal class ServiceIndex
    {
        [JsonProperty(PropertyName = "version")]
        public string Version
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "resources")]
        public ServiceIndexResource[] Resources
        {
            get;
            private set;
        }

        /// <summary>
        /// Retrieves the resource id (@id) for the given type.
        /// </summary>
        /// <param name="type">The resource type to get the id for. e.g. RegistrationsBaseUrl</param>
        /// <param name="id">The @id for the resource. e.g. https://api.nuget.org/v3/registration1/</param>
        /// <returns>True if the @id was found. Otherwise false.</returns>
        public bool TryGetResourceId(string type, out Uri id)
        {
            try
            {
                id = this.Resources.FirstOrDefault(r => r.Type != null && r.Type.Equals(type))?.Id;
            }
            catch
            {
                id = null;
            }

            return id != null;
        }

        /// <summary>
        /// Creates a ServiceIndex object from the contents of a URL.
        /// </summary>
        /// <param name="serviceIndexUrl">The URL that returns the service index json.</param>
        /// <returns>A ServiceIndex which represents the contents return by the URL.</returns>
        public static ServiceIndex Deserialize(Uri serviceIndexUrl)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString(serviceIndexUrl);
                return ServiceIndex.Deserialize(json);
            }
        }

        /// <summary>
        /// Creates a ServiceIndex object from the contents of a json string.
        /// </summary>
        /// <param name="json">The json string that defines the service index.</param>
        /// <returns>A ServiceIndex which represents the json string.</returns>
        public static ServiceIndex Deserialize(string json)
        {
            ServiceIndex item = JsonConvert.DeserializeObject<ServiceIndex>(json);

            // Do some basic validation
            if (item == null || item.Resources == null)
            {
                throw new ArgumentOutOfRangeException("The json string was not a service index.");
            }

            Uri registrationUri = item.Resources.FirstOrDefault(r => r.Type != null && r.Type.Equals("RegistrationsBaseUrl", StringComparison.OrdinalIgnoreCase))?.Id;
            if (registrationUri == null)
            {
                throw new ArgumentOutOfRangeException("The json string did not have a resource with @type = RegistrationsBaseUrl");
            }

            if (String.IsNullOrWhiteSpace(registrationUri.OriginalString))
            {
                throw new ArgumentOutOfRangeException("The json string did not have an @id value for the resource @type = RegistrationsBaseUrl");
            }

            return item;
        }
    }
}


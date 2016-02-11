// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ng.Models;
using NuGet.Services.Metadata.Catalog;

namespace Ng
{
    /// <summary>
    /// Container type for all the NuGet service endpoints.
    /// </summary>
    class NugetServiceEndpoints
    {
        /// <summary>
        /// Creates a new NugetServiceUrl type using the service endpoints defined in the serviceIndexUrl provided.
        /// </summary>
        /// <param name="serviceIndexUrl">The service index endpoint which contains the definition of the NuGet services. e.g. https://api.nuget.org/v3/index.json</param>
        public NugetServiceEndpoints(Uri serviceIndexUrl)
        {
            this.InitializeAsync(serviceIndexUrl).Wait();
        }

        /// <summary>
        /// The root service endpoint which contains the defintion for all the other endpoints. e.g. https://api.nuget.org/v3/index.json
        /// </summary>
        public Uri ServiceIndexUrl
        {
            get;
            private set;
        }

        /// <summary>
        /// The base URL for the registration endpoints. e.g. https://api.nuget.org/v3/registration1/
        /// </summary>
        public Uri RegistrationBaseUrl
        {
            get;
            private set;
        }

        /// <summary>
        /// Composes the registration URL for the specified package. e.g. https://api.nuget.org/v3/registration1/autofac.mvc2/2.3.2.632.json
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The registration URL for the specified package.</returns>
        public Uri ComposeRegistrationUrl(string packageId)
        {
            // The registration URL looks similar to https://api.nuget.org/v3/registration1/autofac.mvc2/index.json
            string relativePath = $"{packageId}/index.json";

            // The URL path must be lower cased.
            relativePath = relativePath.ToLowerInvariant();

            Uri registrationUrl = new Uri(this.RegistrationBaseUrl, relativePath);
            return registrationUrl;
        }

        /// <summary>
        /// Composes the registration URL for the specified package version. e.g. https://api.nuget.org/v3/registration1/autofac.mvc2/2.3.2.632.json
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <returns>The registration URL for the specified package.</returns>
        public Uri ComposeRegistrationUrl(string packageId, string packageVersion)
        {
            // The registration URL looks similar to https://api.nuget.org/v3/registration1/autofac.mvc2/2.3.2.632.json
            string relativePath = $"{packageId}/{packageVersion}.json";

            // The URL path must be lower cased.
            relativePath = relativePath.ToLowerInvariant();

            Uri registrationUrl = new Uri(this.RegistrationBaseUrl, relativePath);
            return registrationUrl;
        }

        /// <summary>
        /// Sets the service endpoint property values to the ones defined in the specified service index.
        /// </summary>
        /// <param name="serviceIndexUrl">The service index endpoint which contains the endpoints of the NuGet services.</param>
        private async Task InitializeAsync(Uri serviceIndexUrl)
        {
            ServiceIndex serviceIndex;

            using (CollectorHttpClient client = new CollectorHttpClient())
            {
                string serviceIndexText = await client.GetStringAsync(serviceIndexUrl);
                serviceIndex = ServiceIndex.Deserialize(serviceIndexText);
            }

            Uri registrationBaseUrl;
            if (!serviceIndex.TryGetResourceId("RegistrationsBaseUrl", out registrationBaseUrl))
            {
                throw new ArgumentOutOfRangeException("serviceIndexAddress", "The service index does not contain a RegistrationBaseUrl entry.");
            }

            this.RegistrationBaseUrl = registrationBaseUrl;
            this.ServiceIndexUrl = serviceIndexUrl;
        }
    }
}


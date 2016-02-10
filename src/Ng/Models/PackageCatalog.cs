// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ng.Persistence;
using NuGet.Services.Metadata.Catalog.Persistence;
using System.Diagnostics;

namespace Ng.Models
{
    /// <summary>
    /// The PackageCatalog records the latest stable version of each package.
    /// </summary>
    class PackageCatalog : JsonStorageItem
    {
        Object _syncroot = new Object();

        /// <summary>
        /// Creates a new PackageCatalog.
        /// </summary>
        /// <param name="catalog">The address of the NuGet catalog endpoint.</param>
        /// <param name="storage">The Storage object responsible for loading and saving the file.</param>
        /// <param name="address">The storage resource URI to save the package to.</param>
        /// <param name="nugetServiceUrls">The set of NuGet endpoints to use when determining if a package is the latest stable version.</param>
        public PackageCatalog(Uri catalog, IStorage storage, Uri address, NugetServiceEndpoints nugetServiceUrls) : base(storage, address)
        {
            this.Catalog = catalog;
            this.Packages = new SortedList<string, PackageInfo>();
            this.NugetServiceUrls = nugetServiceUrls;
        }

        /// <summary>
        /// The NuGet catalog endpoint.
        /// </summary>
        [JsonProperty("catalog")]
        public Uri Catalog
        {
            get;
            set;
        }

        /// <summary>
        /// The time the file was last saved.
        /// </summary>
        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated
        {
            get;
            set;
        }

        /// <summary>
        /// The list of packages.
        /// </summary>
        [JsonProperty("packages")]
        public SortedList<string, PackageInfo> Packages
        {
            get;
            set;
        }

        /// <summary>
        /// The NuGet services to use to determine if a package is the latest stable version.
        /// </summary>
        NugetServiceEndpoints NugetServiceUrls
        {
            get;
            set;
        }

        /// <summary>
        /// Removes a package from the PackageCatalog.
        /// </summary>
        /// <param name="packageId">The id of the package to remove.</param>
        public void DelistPackage(string packageId)
        {
            // The packages key must be lower case to match the NuGet download count json.
            string key = packageId.ToLowerInvariant();

            lock (this._syncroot)
            {
                this.Packages.Remove(key);
            }
        }

        /// <summary>
        /// Determines of a package is the latest stable version.
        /// </summary>
        /// <param name="item">The package to verify.</param>
        /// <param name="latestStablePackage">If the package is the latest stable version, returns information about the package. Otherwise null.</param>
        /// <returns>Returns true if the package is the latest stable version. Otherwise false.</returns>
        public bool IsLatestStablePackage(CatalogItem item, out PackageInfo latestStablePackage)
        {
            // Try to get the latest version from the storage catalog
            latestStablePackage = this.GetLatestStablePackage(item.PackageId);

            // If the storage catalog didn't contain the package or the package is out-of-date, update
            // the latest stable version from the NuGet registration service.
            if (latestStablePackage == null || latestStablePackage.CommitTimeStamp <= item.CommitTimeStamp)
            {
                RegistrationIndexPackage latestStablePackageRegistration = item.GetLatestStableVersion(this.NugetServiceUrls);

                if (latestStablePackageRegistration == null)
                {
                    // If there isn't a latest stable version for this package, the input item can't be the latest stable version.
                    latestStablePackage = null;
                    return false;
                }
                else
                {
                    // Save the latest stable version.
                    latestStablePackage = this.SetLatestStablePackage(latestStablePackageRegistration.CatalogEntry.PackageId,
                        latestStablePackageRegistration.CatalogEntry.PackageVersion,
                        latestStablePackageRegistration.CommitId,
                        latestStablePackageRegistration.CommitTimeStamp,
                        latestStablePackageRegistration.PackageContent,
                        false);
                }
            }

            // Check if the item is the latest stable version.
            return latestStablePackage.LatestStableVersion.Equals(item.PackageVersion);
        }

        /// <summary>
        /// Sets the latest stable version of a package. If the package already exists in the PackageCatalog,
        /// it's metadata is updated with the provided values.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <param name="commitId">The commit id.</param>
        /// <param name="commitTimeStamp">The commit timestamp.</param>
        /// <param name="downloadUrl">The download url for the package.</param>
        /// <param name="haveIdx">Indicates if we've created and stored the Idx for this package.</param>
        /// <returns>The package information for the input package.</returns>
        public PackageInfo SetLatestStablePackage(string packageId, string packageVersion, Guid commitId, DateTime commitTimeStamp, Uri downloadUrl, bool haveIdx)
        {
            PackageInfo packageInfo;
            // The packages key must be lower case to match the NuGet download count json.
            string key = packageId.ToLowerInvariant();

            lock (this._syncroot)
            {
                if (!this.Packages.TryGetValue(key, out packageInfo))
                {
                    packageInfo = new PackageInfo();
                    packageInfo.PackageId = packageId;
                }

                packageInfo.LatestStableVersion = packageVersion;
                packageInfo.CommitId = commitId;
                packageInfo.CommitTimeStamp = commitTimeStamp;
                packageInfo.DownloadUrl = downloadUrl;
                packageInfo.HaveIdx = haveIdx;

                this.Packages[key] = packageInfo;
            }

            return packageInfo;
        }

        /// <summary>
        /// Sets the haveIdx field of a package in the PackageCatalog.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <param name="haveIdx">Indicates if we've created and stored the Idx for this package.</param>
        /// <returns>Returns ture if the package information was updated. Otherwise, false.</returns>
        public bool UpdateLatestStablePackage(string packageId, string packageVersion, bool haveIdx)
        {
            PackageInfo packageInfo;
            // The packages key must be lower case to match the NuGet download count json.
            string key = packageId.ToLowerInvariant();

            lock (this._syncroot)
            {
                // Try to get the package with the exact package id and version. 
                if (this.Packages.TryGetValue(key, out packageInfo))
                {
                    if (packageInfo.LatestStableVersion == packageVersion)
                    {
                        packageInfo.HaveIdx = haveIdx;
                        this.Packages[key] = packageInfo;
                        return true;
                    }
                }
            }

            // If the PackageCatalog doesn't contain the the package/version, there's nothing to update.
            return false;
        }

        private PackageInfo GetLatestStablePackage(string packageId)
        {
            PackageInfo packageInfo;
            // The packages key must be lower case to match the NuGet download count json.
            string key = packageId.ToLowerInvariant();

            this.Packages.TryGetValue(key, out packageInfo);
            return packageInfo;
        }

        public override Task SaveAsync(CancellationToken cancellationToken)
        {
            this.LastUpdated = DateTime.Now;
            return base.SaveAsync(cancellationToken);
        }

        public override async Task LoadAsync(Uri address, IStorage storage, CancellationToken cancellationToken)
        {
            string json = await storage.LoadString(address, cancellationToken);

            // If this is the first time we're running the crawler, there won't be any file to load.
            if (json == null)
            {
                Trace.TraceInformation("No package catalog to load.");
                return;
            }

            PackageCatalog item = JsonConvert.DeserializeObject<PackageCatalog>(json);

            this.Catalog = item.Catalog;
            this.LastUpdated = item.LastUpdated;

            if (item.Packages != null)
            {
                this.Packages = item.Packages;
            }
        }
    }
}


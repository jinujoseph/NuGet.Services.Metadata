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

namespace Ng.Models
{
    class PackageCatalog : JsonStorageItem
    {
        Object _syncroot = new Object();

        public PackageCatalog(Uri catalog, Uri address, IStorage storage, NugetServiceEndpoints nugetServiceUrls) : base(address, storage)
        {
            this.Catalog = catalog;
            this.Packages = new SortedList<string, PackageInfo>();
            this.NugetServiceUrls = nugetServiceUrls;
        }

        [JsonProperty("catalog")]
        public Uri Catalog
        {
            get;
            set;
        }

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated
        {
            get;
            set;
        }

        [JsonProperty("packages")]
        public SortedList<string, PackageInfo> Packages
        {
            get;
            set;
        }

        NugetServiceEndpoints NugetServiceUrls
        {
            get;
            set;
        }

        public void DelistPackage(string packageId)
        {
            lock (this._syncroot)
            {
                this.Packages.Remove(packageId);
            }
        }

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

        public PackageInfo SetLatestStablePackage(string packageId, string packageVersion, Guid commitId, DateTime commitTimeStamp, Uri downloadUrl, bool haveIdx)
        {
            lock (this._syncroot)
            {
                if (this.Packages.ContainsKey(packageId))
                {
                    this.Packages[packageId].LatestStableVersion = packageVersion;
                    this.Packages[packageId].CommitId = commitId;
                    this.Packages[packageId].CommitTimeStamp = commitTimeStamp;
                    this.Packages[packageId].DownloadUrl = downloadUrl;
                    this.Packages[packageId].HaveIdx = haveIdx;
                }
                else
                {
                    PackageInfo packageInfo = new PackageInfo();
                    packageInfo.PackageId = packageId;
                    packageInfo.LatestStableVersion = packageVersion;
                    packageInfo.CommitId = commitId;
                    packageInfo.CommitTimeStamp = commitTimeStamp;
                    packageInfo.DownloadUrl = downloadUrl;
                    packageInfo.HaveIdx = haveIdx;

                    this.Packages.Add(packageInfo.PackageId, packageInfo);
                }

                return this.Packages[packageId];
            }
        }

        private PackageInfo GetLatestStablePackage(string packageId)
        {
            lock (this._syncroot)
            {
                if (this.Packages.ContainsKey(packageId))
                {
                    return this.Packages[packageId];
                }
                else
                {
                    return null;
                }
            }
        }

        public override async Task LoadAsync(Uri address, IStorage storage, CancellationToken cancellationToken)
        {
            string json = await storage.LoadString(address, cancellationToken);

            if (json == null)
            {
                throw new ArgumentOutOfRangeException("The address did not contain a storage file.");
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


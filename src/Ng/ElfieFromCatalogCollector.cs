// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using Catalog = NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Ng.Models;
using Ng.Elfie;
using System.Net;

namespace Ng
{
    /// <summary>
    /// Creates Elfie index (Idx) files for NuGet packages.
    /// </summary>
    class ElfieFromCatalogCollector
    {
        Storage _storage;
        int _maxThreads;
        NugetServiceEndpoints _nugetServiceUrls;
        string _tempPath;
        Version _indexerVersion;
        Version _mergerVersion;
        PackageCatalog _packageCatalog;
        Uri _downloadCountsUri;
        double _downloadPercentage;

        public ElfieFromCatalogCollector(Version indexerVersion, Version mergerVersion, NugetServiceEndpoints nugetServiceUrls, Uri downloadCountsUri, double downloadPercentage, Storage storage, int maxThreads, string tempPath, PackageCatalog packageCatalog)
        {
            this._storage = storage;
            this._maxThreads = maxThreads;
            this._tempPath = Path.GetFullPath(tempPath);
            this._indexerVersion = indexerVersion;
            this._mergerVersion = mergerVersion;
            this._packageCatalog = packageCatalog;
            this._nugetServiceUrls = nugetServiceUrls;
            this._downloadCountsUri = downloadCountsUri;
            this._downloadPercentage = downloadPercentage;
        }

        /// <summary>
        /// Processes the next set of NuGet packages from the catalog.
        /// </summary>
        /// <returns>True if the batch processing should continue. Otherwise false.</returns>
        public async Task<bool> Run(CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity OnProcessBatch");

            try
            {
                // Load the download counts  
                JArray downloadJson = FetchDownloadCounts(this._downloadCountsUri);

                // Get the download counts for each package that we have an idx for.  
                long totalDownloadCount = 0;
                Dictionary<string, long> packageDownloadCounts = GetPackageDownloadCounts(packageCatalog, downloadJson, out totalDownloadCount);

                // Get the packages to include in the ardb.  
                long downloadCountToCover = (long)(totalDownloadCount * options.DownloadPercentage);
                IEnumerable<string> packagesToIncludeInArdb = GetPackagesToIncludeInArdb(packageDownloadCounts, downloadCountToCover);

                // Convert the package ids to PackageInfo objects. We need these because they contain the  
                // version number of the packages to retrieve.  
                List<PackageInfo> packageInfosToInclude = new List<PackageInfo>();
                foreach (string packageId in packagesToIncludeInArdb)
                {
                    PackageInfo packageInfo = packageCatalog.Packages[packageId];
                    packageInfosToInclude.Add(packageInfo);
                }

                // Process each of the filterd packages.
                await ProcessCatalogItemsAsync(catalogItems, cancellationToken);
            }
            catch (System.Net.WebException e)
            {
                System.Net.HttpWebResponse response = e.Response as System.Net.HttpWebResponse;
                if (response != null && response.StatusCode == System.Net.HttpStatusCode.BadGateway)
                {
                    // If the response is a bad gateway, it's likely a transient error. Return false so we'll
                    // sleep in Catalog2Elfie and try again after the interval elapses.
                    return false;
                }
                else
                {
                    // If it's any other error, rethrow the exception. This will stop the application so
                    // the issue can be addressed.
                    throw;
                }
            }

            Trace.TraceInformation("#StopActivity OnProcessBatch");

            return true;
        }

        /// <summary>
        /// Downloads the catalog entries for a set of NuGet packages.
        /// </summary>
        /// <param name="client">The HttpClient which will download the catalog entires.</param>
        /// <param name="items">The list of packages to download catalog entires for.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The catalog entires for the packages specified by the 'items' parameter.</returns>
        /// <remarks>The catalog entries are a json files which describe basic information about a package.
        /// For example: https://api.nuget.org/v3/catalog0/data/2015.02.02.16.48.21/angularjs.1.0.2.json
        /// </remarks>
        async Task<IEnumerable<CatalogItem>> FetchCatalogItems(Catalog.CollectorHttpClient client, IEnumerable<JToken> items, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity FetchCatalogItems");

            IList<Task<CatalogItem>> tasks = new List<Task<CatalogItem>>();

            foreach (JToken item in items)
            {
                Uri catalogItemUri = item["@id"].ToObject<Uri>();

                tasks.Add(Task.Run<CatalogItem>(() =>
                {
                    string catalogItemJson = client.GetStringAsync(catalogItemUri, cancellationToken).Result;
                    CatalogItem catalogItem = CatalogItem.Deserialize(catalogItemJson);
                    return catalogItem;
                }));

            }

            await Task.WhenAll(tasks);

            IEnumerable<CatalogItem> catalogItems = tasks.Select(t => t.Result);

            Trace.TraceInformation("#StopActivity FetchCatalogItems");

            return catalogItems;
        }

        /// <summary>
        /// Enumerates through the catalog enties and processes each entry.
        /// </summary>
        /// <param name="catalogItems">The list of catalog entires to process.</param>
        async Task ProcessCatalogItemsAsync(IEnumerable<CatalogItem> catalogItems, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessCatalogItems");

            // This is a mapping between the packages we're processing and their effect on
            // the package catalog (the file which stores the latest stable version of the package.)
            // We'll use this later to update the package catalog appropriately.
            Dictionary<Uri, CommitAction> packageCommitActions = new Dictionary<Uri, CommitAction>();

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = this._maxThreads,
            };

            Parallel.ForEach(catalogItems, options, catalogItem =>
            {
                Trace.TraceInformation("Processing CatalogItem {0}", catalogItem.PackageId);

                if (catalogItem.IsPackageDetails)
                {
                    CommitAction commitAction = ProcessPackageDetailsAsync(catalogItem, cancellationToken).Result;

                    if (commitAction != CommitAction.None)
                    {
                        lock (packageCommitActions)
                        {
                            packageCommitActions.Add(catalogItem.Id, commitAction);
                        }
                    }
                }
                else if (catalogItem.IsPackageDelete)
                {
                    lock (packageCommitActions)
                    {
                        packageCommitActions.Add(catalogItem.Id, CommitAction.Delist);
                    }
                }
                else
                {
                    Trace.TraceWarning("Unrecognized @type ignoring CatalogItem");
                }
            });

            // Update the package catalog.
            await UpdatePackageCatalogAsync(catalogItems, packageCommitActions, cancellationToken);

            Trace.TraceInformation("#StopActivity ProcessCatalogItems");
        }

        /// <summary>
        /// Process an individual catalog item (NuGet pacakge) which has been added or updated in the catalog
        /// </summary>
        /// <param name="catalogItem">The catalog item to process.</param>
        async Task<CommitAction> ProcessPackageDetailsAsync(CatalogItem catalogItem, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessPackageDetailsAsync " + catalogItem.PackageId + " " + catalogItem.PackageVersion);

            Uri idxFile = null;
            PackageInfo latestStablePackage;

            // Only process the latest stable version of the package.
            if (!catalogItem.IsPrerelease && this._packageCatalog.IsLatestStablePackage(catalogItem, out latestStablePackage))
            {
                // Download and process the package

                Uri packageResourceUri = await this.DownloadPackageAsync(catalogItem, latestStablePackage.DownloadUrl, cancellationToken);

                // If we successfully downloaded the package, create the idx file for the package.  
                if (packageResourceUri != null)
                {
                    idxFile = await this.DecompressAndIndexPackageAsync(packageResourceUri, catalogItem, cancellationToken);
                }
            }

            // The commit action indicates if we've processed the latest stable version of the package.
            CommitAction commitAction = CommitAction.None;
            if (idxFile != null)
            {
                // Since we have the idx file, it must be the latest stable version.
                commitAction = CommitAction.LatestStable;
            }

            Trace.TraceInformation("#StopActivity ProcessPackageDetailsAsync");

            return commitAction;
        }

        /// <summary> 
        /// Downloads a package (nupkg) and saves the package to storage. 
        /// </summary> 
        /// <param name="catalogItem">The catalog data for the package to download.</param> 
        /// <param name="packageDownloadUrl">The download URL for the package to download.</param> 
        /// <returns>The storage resource URL for the saved package.</returns> 
        async Task<Uri> DownloadPackageAsync(CatalogItem catalogItem, Uri packageDownloadUrl, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity DownloadPackageAsync " + catalogItem.PackageId + " " + catalogItem.PackageVersion);

            Uri packageResourceUri = null;

            try
            {
                // Get the package file name from the download URL. 
                string packageFileName = Path.GetFileName(packageDownloadUrl.LocalPath);

                // This is the storage path for the package.  
                packageResourceUri = this._storage.ComposePackageResourceUrl(catalogItem.PackageId, catalogItem.PackageVersion, packageFileName);

                // Check if we already downloaded the package in a previous run. 
                using (StorageContent packageStorageContent = await this._storage.Load(packageResourceUri, cancellationToken))
                {
                    if (packageStorageContent == null)
                    {
                        // The storage doesn't contain the package, so we have to download and save it. 
                        Trace.TraceInformation("Saving nupkg to " + packageResourceUri.AbsoluteUri);
                        this._storage.SaveUrlContents(packageDownloadUrl, packageResourceUri);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());

                WebException webException = e as WebException;
                if (webException != null && ((HttpWebResponse)webException.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    // We received a 404 (file not found) when trying to download the file.   
                    // There's not much we can do here since the package download URL doesn't exist.  
                    // Return null, which indicates that the package doesn't exist, and continue.  
                    Trace.TraceError($"The package download URL returned a 404. {packageDownloadUrl}");
                    return null;
                }

                // If something went wrong, we should delete the package from storage so we don't have partially downloaded files. 
                if (packageResourceUri != null)
                {
                    await this._storage.Delete(packageResourceUri, cancellationToken);
                }

                throw;
            }

            Trace.TraceInformation("#StopActivity DownloadPackageAsync");

            return packageResourceUri;
        }

        /// <summary>
        /// Decompresses a nupkg file to a temp directory, runs elfie to create an Idx file for the package, and stores the Idx file.
        /// </summary>
        async Task<Uri> DecompressAndIndexPackageAsync(Uri packageResourceUri, CatalogItem catalogItem, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity DecompressAndIndexPackageAsync " + catalogItem.PackageId + " " + catalogItem.PackageVersion);

            Uri idxResourceUri = null;

            // This is the pointer to the package file in storage
            Trace.TraceInformation("Loading package from storage.");
            StorageContent packageStorage = this._storage.Load(packageResourceUri, new CancellationToken()).Result;

            // This is the temporary directory that we'll work in.
            string tempDirectory = Path.Combine(this._tempPath, Guid.NewGuid().ToString());
            Trace.TraceInformation($"Temp directory: {tempDirectory}.");

            try
            {
                // Create the temp directory and expand the nupkg file
                Directory.CreateDirectory(tempDirectory);

                this.ExpandPackage(packageStorage, tempDirectory);
                string idxFile = this.CreateIdxFile(catalogItem.PackageId, catalogItem.PackageVersion, tempDirectory);

                if (idxFile == null)
                {
                    Trace.TraceInformation("The idx file was not created.");
                }
                else
                {
                    Trace.TraceInformation("Saving the idx file.");

                    idxResourceUri = this._storage.ComposeIdxResourceUrl(this._indexerVersion, catalogItem.PackageId, catalogItem.PackageVersion);
                    this._storage.SaveFileContents(idxFile, idxResourceUri);
                }
            }
            catch (ZipException ze)
            {
                // The package couldn't be decompressed.  
                Trace.TraceError(ze.ToString());
                Trace.TraceError($"Could not decompress the package. {packageResourceUri}");
                idxResourceUri = null;
            }
            catch
            {
                // The idx creation failed, so delete any files which were saved to storage.  
                if (idxResourceUri != null)
                {
                    await this._storage.Delete(idxResourceUri, cancellationToken);
                    idxResourceUri = null;
                }

                throw;
            }
            finally
            {
                Trace.TraceInformation("Deleting the temp directory.");
                Directory.Delete(tempDirectory, true);
            }

            Trace.TraceInformation("#StopActivity DecompressAndIndexPackageAsync");

            return idxResourceUri;
        }

        /// <summary>
        /// Expands the package file to the temp directory.
        /// </summary>
        void ExpandPackage(StorageContent packageStorage, string tempDirectory)
        {
            Trace.TraceInformation("#StartActivity ExpandPackage");

            Trace.TraceInformation("Decompressing package to temp directory.");
            FastZip fastZip = new FastZip();
            fastZip.ExtractZip(packageStorage.GetContentStream(), tempDirectory, FastZip.Overwrite.Always, null, ".*", ".*", true, true);

            Trace.TraceInformation("#StopActivity ExpandPackage");
        }

        /// <summary>
        /// Creates and stores the Idx file.
        /// </summary>
        string CreateIdxFile(string packageId, string packageVersion, string tempDirectory)
        {
            Trace.TraceInformation("#StartActivity CreateIdxFile " + packageId + " " + packageVersion);

            Trace.TraceInformation("Creating the idx file.");

            ElfieCmd cmd = new ElfieCmd(this._indexerVersion);
            string idxFile = cmd.RunIndexer(tempDirectory, packageId, packageVersion);

            Trace.TraceInformation("#StopActivity CreateIdxFile");

            return idxFile;
        }

        /// <summary>
        /// Updates the package catalog to indicate which idx files were created
        /// </summary>
        /// <param name="catalogItems">The list of packages that were processed</param>
        /// <param name="packageCommitActions">The mapping which indicates if the package is the latest stable version.</param>
        async Task UpdatePackageCatalogAsync(IEnumerable<CatalogItem> catalogItems, Dictionary<Uri, CommitAction> packageCommitActions, CancellationToken cancellationToken)
        {
            // We need to process the packages in chronological order.
            foreach (CatalogItem catalogItem in catalogItems.OrderBy(c => c.CommitTimeStamp))
            {
                if (packageCommitActions.ContainsKey(catalogItem.Id))
                {
                    CommitAction action = packageCommitActions[catalogItem.Id];

                    switch (action)
                    {
                        case CommitAction.Delist:
                            this._packageCatalog.DelistPackage(catalogItem.PackageId);
                            break;
                        case CommitAction.LatestStable:
                            this._packageCatalog.UpdateLatestStablePackage(catalogItem.PackageId, catalogItem.PackageVersion, true);
                            break;
                        case CommitAction.None:
                        default:
                            break;
                    }
                }
            }

            await this._packageCatalog.SaveAsync(cancellationToken);
        }

        JArray FetchDownloadCounts(Uri downloadJsonUri)
        {
            JArray downloadJson;
            using (WebClient webClient = new WebClient())
            {
                string downloadText = webClient.DownloadString(downloadJsonUri);
                downloadJson = JArray.Parse(downloadText);
            }

            // Basic validation, just check that the package counts are about the right number.
            if (downloadJson.Count < 40000)
            {
                throw new ArgumentOutOfRangeException("downloadJsonUri", "The download count json file which was downloaded did not contain all the package download data.");
            }

            return downloadJson;
        }

        Dictionary<string, long> GetPackageDownloadCounts(Models.PackageCatalog packageCatalog, JArray downloadJson, out long totalDownloadCount)
        {
            Dictionary<string, long> packageDownloadCounts = new Dictionary<string, long>();
            totalDownloadCount = 0;

            foreach (JArray packageDownloadJson in downloadJson)
            {
                string packageId = packageDownloadJson.First.Value<string>();

                PackageInfo packageInfo;
                if (packageCatalog.Packages.TryGetValue(packageId, out packageInfo))
                {
                    if (packageInfo.HaveIdx)
                    {
                        // Get the package counts.  
                        int downloadCount = 0;
                        foreach (JArray versionDownloadJson in packageDownloadJson.Children<JArray>())
                        {
                            string version = versionDownloadJson[0].Value<string>();
                            int versionCount = versionDownloadJson[1].Value<int>();

                            downloadCount += versionCount;
                            totalDownloadCount += versionCount;
                        }

                        packageDownloadCounts[packageId] = downloadCount;
                    }
                }
            }

            return packageDownloadCounts;
        }

        IEnumerable<string> GetPackagesToIncludeInArdb(Dictionary<string, long> packageDownloadCounts, long downloadCountToCover)
        {
            Dictionary<string, long> packagesToInclude = new Dictionary<string, long>();
            long downloadCountSoFar = 0;

            // Include the popular packages until the threshold is reached.  
            foreach (var packageCount in packageDownloadCounts.OrderByDescending(d => d.Value))
            {
                // If we've reached our threshold, stop counting.  
                if (downloadCountSoFar >= downloadCountToCover)
                {
                    break;
                }

                downloadCountSoFar += packageCount.Value;
                packagesToInclude[packageCount.Key] = packageCount.Value;
            }

            // Convert the download counts into their log 10 values.  
            IEnumerable<string> keys = new List<string>(packagesToInclude.Keys);
            foreach (string key in keys)
            {
                long count = packagesToInclude[key];
                long log;

                if (count <= 0)
                {
                    log = 0;
                }
                else
                {
                    log = (long)Math.Log10(count);
                }

                packagesToInclude[key] = log;
            }

            // Sort the list, first by download count, then by id  
            var orderedPackages = packagesToInclude.OrderByDescending(item => item.Value).ThenBy(item => item.Key);

            // Return the list of package ids which have been sorted.  
            return orderedPackages.Select(item => item.Key);
        }

        IEnumerable<string> StageIdxFiles(IEnumerable<PackageInfo> packages, IStorage storage, Version idxStorageVersion, string outputDirectory)
        {
            List<string> idxFileList = new List<string>();
            Directory.CreateDirectory(outputDirectory);

            foreach (PackageInfo packageInfo in packages)
            {
                Uri idxResourceUri = storage.ComposeIdxResourceUrl(idxStorageVersion, packageInfo.PackageId, packageInfo.LatestStableVersion);

                StorageContent idxContent = storage.Load(idxResourceUri, new CancellationToken()).Result;

                string idxFilePath = Path.Combine(outputDirectory, Path.GetFileName(idxResourceUri.LocalPath));
                idxFileList.Add(idxFilePath);

                using (Stream idxStream = idxContent.GetContentStream())
                {
                    using (FileStream fileStream = File.OpenWrite(idxFilePath))
                    {
                        idxStream.CopyTo(fileStream);
                    }
                }
            }

            return idxFileList;
        }

        string CreateArdbFile(Version toolVersion, IEnumerable<string> idxList, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            // Save the idx file list to a file.  
            string idxListFile = Path.Combine(outputDirectory, "IDXList.txt");
            File.WriteAllLines(idxListFile, idxList);

            ElfieCmd cmd = new ElfieCmd(toolVersion);
            string ardbFile = cmd.RunMerger(idxListFile, outputDirectory);

            return ardbFile;
        }

    }
}

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

namespace Ng
{
    /// <summary>
    /// Creates Elfie index (Idx) files for NuGet packages.
    /// </summary>
    class ElfieFromCatalogCollector : Catalog.CommitCollector
    {
        Storage _storage;
        int _maxThreads;
        NugetServiceEndpoints _nugetServiceUrls;
        string _tempPath;
        Version _indexerVersion;
        PackageCatalog _packageCatalog;

        public ElfieFromCatalogCollector(Version indexerVersion, Uri index, NugetServiceEndpoints nugetServiceUrls, Storage storage, int maxThreads, string tempPath, PackageCatalog packageCatalog, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            this._storage = storage;
            this._maxThreads = maxThreads;
            this._tempPath = Path.GetFullPath(tempPath);
            this._indexerVersion = indexerVersion;
            this._packageCatalog = packageCatalog;
            this._nugetServiceUrls = nugetServiceUrls;
        }

        /// <summary>
        /// Processes the next set of NuGet packages from the catalog.
        /// </summary>
        /// <returns>True if the batch processing should continue. Otherwise false.</returns>
        protected override async Task<bool> OnProcessBatch(Catalog.CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity OnProcessBatch");

            // Get the catalog entries for the packages in this batch
            IEnumerable<CatalogItem> catalogItems = await FetchCatalogItems(client, items, cancellationToken);

            // Filter the list to only items we want to process
            IEnumerable<Tuple<CatalogItem, PackageInfo>> filteredItems = await FilterCatalogItems(catalogItems, cancellationToken);

            // Process each of the filterd packages.
            await ProcessCatalogItemsAsync(filteredItems, cancellationToken);

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

        async Task<IEnumerable<Tuple<CatalogItem, PackageInfo>>> FilterCatalogItems(IEnumerable<CatalogItem> items, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity FilterCatalogItems");

            IList<Task<Tuple<CatalogItem, PackageInfo>>> tasks = new List<Task<Tuple<CatalogItem, PackageInfo>>>();

            // Filter the list of items to only packages we want to process.
            // This includes any latest stable package or any package delete.
            // All other packages can be ignored.
            foreach (CatalogItem item in items)
            {
                tasks.Add(Task.Run<Tuple<CatalogItem, PackageInfo>>(() =>
                {
                    // We have to process all package deletes.
                    if (item.IsPackageDelete)
                    {
                        return Tuple.Create<CatalogItem, PackageInfo>(item, null);
                    }

                    PackageInfo latestStablePackage;
                    if (this._packageCatalog.IsLatestStablePackage(item, out latestStablePackage))
                    {
                        return Tuple.Create(item, latestStablePackage);
                    }

                    return null;
                }));
            }

            await Task.WhenAll(tasks);

            IEnumerable<Tuple<CatalogItem, PackageInfo>> catalogItems = tasks.Select(t => t.Result).Where(p => p != null);

            await this._packageCatalog.SaveAsync(cancellationToken);

            Trace.TraceInformation("#StopActivity FilterCatalogItems");

            return catalogItems;
        }

        /// <summary>
        /// Enumerates through the catalog enties and processes each entry.
        /// </summary>
        /// <param name="catalogItems">The list of catalog entires to process.</param>
        async Task ProcessCatalogItemsAsync(IEnumerable<Tuple<CatalogItem, PackageInfo>> catalogItems, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessCatalogItems");

            Dictionary<Uri, CommitAction> packageCommitActions = new Dictionary<Uri, CommitAction>();

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = this._maxThreads,
            };

            Parallel.ForEach(catalogItems, options, catalogItem =>
            {
                CatalogItem item = catalogItem.Item1;
                PackageInfo packageInfo = catalogItem.Item2;

                Trace.TraceInformation("Processing CatalogItem {0}", item.PackageId);

                if (item.IsPackageDetails)
                {
                    try
                    {
                        Uri idxResourceUri = ProcessPackageDetailsAsync(item, packageInfo.DownloadUrl, cancellationToken).Result;

                        lock (packageCommitActions)
                        {
                            if (idxResourceUri == null)
                            {
                                packageCommitActions.Add(item.Id, CommitAction.None);
                            }
                            else
                            {
                                packageCommitActions.Add(item.Id, CommitAction.LatestStable);
                            }
                        }
                    }
                    catch (AggregateException ae)
                    {
                        AggregateException innerAe = ae.InnerException as AggregateException;

                        if (innerAe == null)
                        {
                            throw;
                        }
                        else
                        {
                            System.Net.Http.HttpRequestException hre = innerAe.InnerException as System.Net.Http.HttpRequestException;

                            if (hre == null)
                            {
                                throw;
                            }
                            else
                            {
                                Trace.TraceError(hre.Message);
                                Trace.TraceError($"Skipping package {item.PackageId} {item.PackageVersion}.");

                                packageCommitActions.Add(item.Id, CommitAction.None);
                            }
                        }
                    }
                }
                else if (item.IsPackageDelete)
                {
                    lock (packageCommitActions)
                    {
                        packageCommitActions.Add(item.Id, CommitAction.Delist);
                    }
                }
                else
                {
                    Trace.TraceWarning("Unrecognized @type ignoring CatalogItem");

                    lock (packageCommitActions)
                    {
                        packageCommitActions.Add(item.Id, CommitAction.None);
                    }
                }
            });

            foreach (Tuple<CatalogItem, PackageInfo> catalogItem in catalogItems.OrderBy(t => t.Item1.CommitTimeStamp))
            {
                CatalogItem item = catalogItem.Item1;
                PackageInfo packageInfo = catalogItem.Item2;

                if (packageCommitActions.ContainsKey(item.Id))
                {
                    CommitAction action = packageCommitActions[item.Id];

                    switch (action)
                    {
                        case CommitAction.Delist:
                            this._packageCatalog.DelistPackage(item.PackageId);
                            break;
                        case CommitAction.LatestStable:
                            this._packageCatalog.SetLatestStablePackage(item.PackageId, item.PackageVersion, item.CommitId, item.CommitTimeStamp, packageInfo.DownloadUrl, true);
                            break;
                        case CommitAction.None:
                        default:
                            break;
                    }
                }
            }

            await this._packageCatalog.SaveAsync(cancellationToken);

            Trace.TraceInformation("#StopActivity ProcessCatalogItems");
        }

        /// <summary>
        /// Process an individual catalog item (NuGet pacakge) which has been added or updated in the catalog
        /// </summary>
        /// <param name="catalogItem">The catalog item to process.</param>
        async Task<Uri> ProcessPackageDetailsAsync(CatalogItem catalogItem, Uri packageDownloadUrl, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessPackageDetailsAsync " + catalogItem.PackageId + " " + catalogItem.PackageVersion);

            // Download and process the package

            Uri packageResourceUri = await this.DownloadPackageAsync(catalogItem, packageDownloadUrl, cancellationToken);
            Uri idxFile = await this.DecompressAndIndexPackageAsync(packageResourceUri, catalogItem, cancellationToken);

            Trace.TraceInformation("#StopActivity ProcessPackageDetailsAsync");

            return idxFile;
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
            catch
            {
                // TODO: Clean up any files that were written to storage.
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
    }
}

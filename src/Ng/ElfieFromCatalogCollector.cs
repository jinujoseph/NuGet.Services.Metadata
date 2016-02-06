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
    public class ElfieFromCatalogCollector : Catalog.CommitCollector
    {
        Storage _storage;
        int _maxThreads;
        NugetServiceEndpoints _nugetServiceUrls;
        string _tempPath;
        string _indexerVersion;

        public ElfieFromCatalogCollector(string indexerVersion, Uri index, Storage storage, int maxThreads, string tempPath, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            this._storage = storage;
            this._maxThreads = maxThreads;
            this._tempPath = Path.GetFullPath(tempPath);
            this._indexerVersion = indexerVersion;

            Uri serviceIndexUrl = NugetServiceEndpoints.ComposeServiceIndexUrlFromCatalogIndexUrl(index);
            this._nugetServiceUrls = new NugetServiceEndpoints(serviceIndexUrl);
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

            // Process each of the packages.
            await ProcessCatalogItemsAsync(catalogItems, cancellationToken);

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

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = this._maxThreads,
            };

            Parallel.ForEach(catalogItems, options, catalogItem =>
            {
                Trace.TraceInformation("Processing CatalogItem {0}", catalogItem.Id);

                if (catalogItem.IsPackageDetails)
                {
                    ProcessPackageDetailsAsync(catalogItem, cancellationToken).Wait();
                }
                else if (catalogItem.IsPackageDelete)
                {
                    ProcessPackageDeleteAsync(catalogItem, cancellationToken).Wait();
                }
                else
                {
                    Trace.TraceWarning("Unrecognized @type ignoring CatalogItem");
                }
            });

            Trace.TraceInformation("#StopActivity ProcessCatalogItems");
        }

        /// <summary>
        /// Process an individual catalog item (NuGet pacakge) which has been added or updated in the catalog
        /// </summary>
        /// <param name="catalogItem">The catalog item to process.</param>
        async Task ProcessPackageDetailsAsync(CatalogItem catalogItem, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessPackageDetailsAsync " + catalogItem.Id + " " + catalogItem.PackageVersion);

            // Download the registration json file.  
            // The registration file will tell if the package is listed as-well-as provide the package download URL.  
            RegistrationItem registrationItem = catalogItem.GetRegistrationItem(this._nugetServiceUrls);

            // We only need to process listed packages  
            if (registrationItem.Listed)
            {
                Uri packageResourceUri = await this.DownloadPackageAsync(catalogItem, registrationItem.PackageContent, cancellationToken);
                Uri idxFile = DecompressAndIndexNupkg(packageResourceUri, catalogItem);
            }

            Trace.TraceInformation("#StopActivity ProcessPackageDetailsAsync");

        }

        /// <summary>
        /// Process an individual catalog item (NuGet pacakge) which has been deleted from the catalog
        /// </summary>
        /// <param name="catalogItem">The catalog item to process.</param>
        async Task ProcessPackageDeleteAsync(CatalogItem catalogItem, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessPackageDeleteAsync " + catalogItem.Id + " " + catalogItem.PackageVersion);

            Trace.TraceInformation("#StopActivity ProcessPackageDeleteAsync");
        }

        /// <summary> 
        /// Downloads a package (nupkg) and saves the package to storage. 
        /// </summary> 
        /// <param name="catalogItem">The catalog data for the package to download.</param> 
        /// <param name="packageDownloadUrl">The download URL for the package to download.</param> 
        /// <returns>The storage resource URL for the saved package.</returns> 
        async Task<Uri> DownloadPackageAsync(CatalogItem catalogItem, Uri packageDownloadUrl, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity DownloadPackageAsync " + catalogItem.Id + " " + catalogItem.PackageVersion);

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
        Uri DecompressAndIndexNupkg(Uri packageResourceUri, CatalogItem catalogItem)
        {
            Uri idxResourceUri = null;

            // This is the pointer to the nupkg file in storage
            StorageContent packageStorage = this._storage.Load(packageResourceUri, new CancellationToken()).Result;

            // This is the temporary directory that we'll work in.
            string tempDirectory = Path.Combine(this._tempPath, Guid.NewGuid().ToString());

            // This is where elfie will create the Idx file.
            string tempIdxFile = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(packageResourceUri.LocalPath) + ".idx");

            try
            {
                // Create the temp directory and expand the nupkg file
                Directory.CreateDirectory(tempDirectory);

                FastZip fastZip = new FastZip();
                fastZip.ExtractZip(packageStorage.GetContentStream(), tempDirectory, FastZip.Overwrite.Always, null, ".*", ".*", true, true);

                // Create and store the Idx file.
                string idxFile = this.CreateIdxFile(tempDirectory, catalogItem.PackageId, catalogItem.PackageVersion);
            }
            catch
            {
                // TODO: Clean up any files that were written to storage.
                throw;
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }

            return idxResourceUri;
        }

        /// <summary>
        /// Creates and stores the Idx file.
        /// </summary>
        string CreateIdxFile(string tempDirectory, string packageId, string packageVersion)
        {
            ElfieCmd cmd = new ElfieCmd(this._indexerVersion);

            // The resource URI for the idx file we're about to create.
            string idxFile = cmd.RunIndexer(tempDirectory, packageId, packageVersion);

            return idxFile;

            //// TODO: handle error codes, kill app if still running, etc
            //if (cmd.HasExited && cmd.ExitCode == 0)
            //{
            //    string idxFile = Directory.GetFiles(idxDirectory, "*.idx").FirstOrDefault();
            //    if (idxFile != null)
            //    {
            //        using (FileStream idxStream = File.OpenRead(idxFile))
            //        {
            //            // resourceUri = file:///C:/NuGet//Packages/Autofac.Mvc2/2.3.2.632/autofac.mvc2.2.3.2.632.nupkg
            //            string resourceUriAddress = this._storage.BaseAddress + "/Idx/" + packageId + "/" + packageVersion + "/" + Path.GetFileName(idxFile);
            //            idxResourceUri = new Uri(resourceUriAddress);
            //            StorageContent content = new StreamStorageContent(idxStream);

            //            Trace.TraceInformation("Saving Idx to " + resourceUriAddress);
            //            this._storage.Save(idxResourceUri, content, new CancellationToken()).Wait();
            //        }
            //    }
            //}
            //else
            //{
            //    if (File.Exists(consoleLogFile))
            //    {
            //        string elfieConsoleText = File.ReadAllText(consoleLogFile);
            //        Trace.TraceError(elfieConsoleText);
            //    }

            //    throw new Exception("Something went wrong running elfie.");
            //}

            //return idxResourceUri;
        }
    }
}

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
        Uri _downloadCountsUri;
        double _downloadPercentage;

        // TODO: delete
        PackageCatalog _packageCatalog;

        public ElfieFromCatalogCollector(Version indexerVersion, Version mergerVersion, NugetServiceEndpoints nugetServiceUrls, Uri downloadCountsUri, double downloadPercentage, Storage storage, int maxThreads, string tempPath)
        {
            this._storage = storage;
            this._maxThreads = maxThreads;
            this._tempPath = Path.GetFullPath(tempPath);
            this._indexerVersion = indexerVersion;
            this._mergerVersion = mergerVersion;
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

                // Get the packages to include in the ardb index
                IEnumerable<RegistrationIndexPackage> sortedPackagesToInclude = GetPackagesToInclude(downloadJson, this._downloadPercentage);

                // Process each of the filterd packages.
                await ProcessItemsAsync(sortedPackagesToInclude, cancellationToken);

                string outputDirectory = Path.Combine(this._tempPath, Guid.NewGuid().ToString());

                try
                {
                    string idxDirectory = Path.Combine(outputDirectory, "idx");
                    string logsDirectory = Path.Combine(outputDirectory, "logs");
                    Directory.CreateDirectory(outputDirectory);
                    Directory.CreateDirectory(idxDirectory);
                    Directory.CreateDirectory(logsDirectory);

                    IEnumerable<string> idxList = StageIdxFiles(sortedPackagesToInclude, this._storage, this._indexerVersion, idxDirectory);
                    string ardbTextFile = CreateArdbFile(this._mergerVersion, idxList, outputDirectory);

                    string version = DateTime.UtcNow.ToString("yyyyMMdd");
                    Uri ardbResourceUri = this._storage.ComposeArdbResourceUrl(this._mergerVersion, $"{version}\\{version}.ardb.txt");
                    this._storage.SaveFileContents(ardbTextFile, ardbResourceUri);
                }
                finally
                {
                    Directory.Delete(outputDirectory, true);
                }
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
        /// Enumerates through the catalog enties and processes each entry.
        /// </summary>
        /// <param name="packages">The list of packages to process.</param>
        async Task ProcessItemsAsync(IEnumerable<RegistrationIndexPackage> packages, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessCatalogItems");

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = this._maxThreads,
            };

            Parallel.ForEach(packages, options, package =>
            {
                Trace.TraceInformation("Processing package {0}", package.CatalogEntry.PackageId);

                Uri idxResourceUri = this._storage.ComposeIdxResourceUrl(this._indexerVersion, package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion);
                StorageContent idxStorageItem = this._storage.Load(idxResourceUri, new CancellationToken()).Result;

                if (idxStorageItem == null)
                {
                    ProcessPackageDetailsAsync(package, cancellationToken).Wait();
                }
                else
                {
                    Trace.TraceInformation($"Idx already exists in storage. {idxResourceUri}");
                }
            });

            Trace.TraceInformation("#StopActivity ProcessCatalogItems");
        }

        /// <summary>
        /// Process an individual catalog item (NuGet pacakge) which has been added or updated in the catalog
        /// </summary>
        /// <param name="package">The catalog item to process.</param>
        async Task ProcessPackageDetailsAsync(RegistrationIndexPackage package, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessPackageDetailsAsync " + package.CatalogEntry.PackageId + " " + package.CatalogEntry.PackageVersion);

            // Download and process the package

            Uri packageResourceUri = await this.DownloadPackageAsync(package, cancellationToken);

            // If we successfully downloaded the package, create the idx file for the package.  
            if (packageResourceUri != null)
            {
                Uri idxFile = await this.DecompressAndIndexPackageAsync(packageResourceUri, package, cancellationToken);
            }

            Trace.TraceInformation("#StopActivity ProcessPackageDetailsAsync");
        }

        /// <summary> 
        /// Downloads a package (nupkg) and saves the package to storage. 
        /// </summary> 
        /// <param name="package">The catalog data for the package to download.</param> 
        /// <param name="packageDownloadUrl">The download URL for the package to download.</param> 
        /// <returns>The storage resource URL for the saved package.</returns> 
        async Task<Uri> DownloadPackageAsync(RegistrationIndexPackage package, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity DownloadPackageAsync " + package.CatalogEntry.PackageId + " " + package.CatalogEntry.PackageVersion);

            Uri packageResourceUri = null;

            try
            {
                // Get the package file name from the download URL. 
                string packageFileName = Path.GetFileName(package.PackageContent.LocalPath);

                // This is the storage path for the package.  
                packageResourceUri = this._storage.ComposePackageResourceUrl(package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion, packageFileName);

                // Check if we already downloaded the package in a previous run. 
                using (StorageContent packageStorageContent = await this._storage.Load(packageResourceUri, cancellationToken))
                {
                    if (packageStorageContent == null)
                    {
                        // The storage doesn't contain the package, so we have to download and save it. 
                        Trace.TraceInformation("Saving nupkg to " + packageResourceUri.AbsoluteUri);
                        this._storage.SaveUrlContents(package.PackageContent, packageResourceUri);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());

                WebException webException = e as WebException;
                if (webException != null && webException.Response != null && ((HttpWebResponse)webException.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    // We received a 404 (file not found) when trying to download the file.   
                    // There's not much we can do here since the package download URL doesn't exist.  
                    // Return null, which indicates that the package doesn't exist, and continue.  
                    Trace.TraceError($"The package download URL returned a 404. {package.PackageContent}");
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
        async Task<Uri> DecompressAndIndexPackageAsync(Uri packageResourceUri, RegistrationIndexPackage package, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity DecompressAndIndexPackageAsync " + package.CatalogEntry.PackageId + " " + package.CatalogEntry.PackageVersion);

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
                string idxFile = this.CreateIdxFile(package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion, tempDirectory);

                if (idxFile == null)
                {
                    Trace.TraceInformation("The idx file was not created.");
                }
                else
                {
                    Trace.TraceInformation("Saving the idx file.");

                    idxResourceUri = this._storage.ComposeIdxResourceUrl(this._indexerVersion, package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion);
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

        JArray FetchDownloadCounts(Uri downloadJsonUri)
        {
            Trace.TraceInformation("Downloading download json file.");

            JArray downloadJson;
            using (WebClient webClient = new WebClient())
            {
                string downloadText = webClient.DownloadString(downloadJsonUri);
                downloadJson = JArray.Parse(downloadText);
            }

            Trace.TraceInformation($"Total packages in download json: {downloadJson.Count.ToString("#,###")}");

            // Basic validation, just check that the package counts are about the right number.
            if (downloadJson.Count < 40000)
            {
                throw new ArgumentOutOfRangeException("downloadJsonUri", "The download count json file which was downloaded did not contain all the package download data.");
            }

            return downloadJson;
        }

        IEnumerable<RegistrationIndexPackage> GetPackagesToInclude(JArray downloadJson, double downloadPercentage)
        {
            long totalDownloadCount;

            // Get the download count for each package
            Dictionary<string, long> packageDownloadCounts = GetDownloadsPerPackage(downloadJson, out totalDownloadCount);

            // Calculate the download threshold
            long downloadCountThreshold = (long)(totalDownloadCount * downloadPercentage);

            // Filter and sort the packages to only the ones we want to include. 
            // Note: packagesToInclude is already sorted in the order the packages should appear in the ardb index.
            IEnumerable<RegistrationIndexPackage> packagesToInclude = GetPackagesToInclude(packageDownloadCounts, downloadCountThreshold);

            return packagesToInclude;
        }

        Dictionary<string, long> GetDownloadsPerPackage(JArray downloadJson, out long totalDownloadCount)
        {
            totalDownloadCount = 0;

            // Get the download count for each package
            Dictionary<string, long> packageDownloadCounts = new Dictionary<string, long>();
            foreach (JArray packageDownloadJson in downloadJson)
            {
                string packageId = packageDownloadJson.First.Value<string>();

                // Get the package counts.  
                long packageDownloadCount = 0;
                foreach (JArray versionDownloadJson in packageDownloadJson.Children<JArray>())
                {
                    string version = versionDownloadJson[0].Value<string>();
                    long versionCount = versionDownloadJson[1].Value<long>();

                    packageDownloadCount += versionCount;
                }

                packageDownloadCounts[packageId] = packageDownloadCount;
                totalDownloadCount += packageDownloadCount;
            }

            Trace.TraceInformation($"Total download count: {totalDownloadCount.ToString("#,###")}");

            return packageDownloadCounts;
        }

        Dictionary<string, RegistrationIndexPackage> GetLatestStableVersion(IEnumerable<string> packageIds)
        {
            Dictionary<string, RegistrationIndexPackage> latestStableVersions = new Dictionary<string, RegistrationIndexPackage>();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = this._maxThreads;

            Parallel.ForEach(packageIds, options, packageId =>
            {
                RegistrationIndexPackage package = this.GetLatestStableVersion(packageId);
                if (package != null)
                {
                    lock (latestStableVersions)
                    {
                        latestStableVersions[packageId] = package;
                    }
                }
            });

            return latestStableVersions;
        }

        RegistrationIndexPackage GetLatestStableVersion(string packageId)
        {
            Trace.TraceInformation($"Determining latest stable version {packageId}.");

            Uri registrationUri = this._nugetServiceUrls.ComposeRegistrationUrl(packageId);
            RegistrationIndex registration = null;

            try
            {
                registration = RegistrationIndex.Deserialize(registrationUri);
            }
            catch (WebException we)
            {
                HttpWebResponse response = we.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    // The URL could not be found (404).
                    // Return null to indicate that we couldn't get the latest stable version.
                    return null;
                }

                // For any other exception, rethrow so we can track the error.
                throw;
            }

            RegistrationIndexPackage latestStableVersion = registration.GetLatestStableVersion();

            if (latestStableVersion == null)
            {
                Trace.TraceWarning($"No latest stable version {packageId}.");
            }
            else
            {
                Trace.TraceInformation($"Latest stable version {packageId} {latestStableVersion.CatalogEntry.PackageVersion}.");
            }

            return latestStableVersion;
        }

        IEnumerable<RegistrationIndexPackage> GetPackagesToInclude(Dictionary<string, long> packageDownloadCounts, long downloadCountThreshold)
        {
            long includedDownloadCount = 0;
            bool thresholdReached = false;
            List<Tuple<RegistrationIndexPackage, int>> includedPackagesWithCounts = new List<Tuple<RegistrationIndexPackage, int>>();

            int batchSize = 500;
            int currentPosition = 0;

            var orderedDownloadCounts = packageDownloadCounts.OrderByDescending(item => item.Value);
            IEnumerable<string> batch = null;

            do
            {
                batch = orderedDownloadCounts.Skip(currentPosition).Take(batchSize).Select(item => item.Key);

                Dictionary<string, RegistrationIndexPackage> latestStableVersions = GetLatestStableVersion(batch);

                foreach (string packageId in batch)
                {
                    long downloadCount = packageDownloadCounts[packageId];
                    RegistrationIndexPackage latestStableVersion;

                    if (latestStableVersions.TryGetValue(packageId, out latestStableVersion))
                    {
                        Trace.TraceInformation($"Included package: {packageId} - {downloadCount.ToString("#,####")}");
                        includedDownloadCount += downloadCount;

                        int base10Count = 0;
                        if (downloadCount != 0)
                        {
                            base10Count = (int)Math.Log10(downloadCount);
                        }

                        includedPackagesWithCounts.Add(Tuple.Create(latestStableVersion, base10Count));
                    }
                    else
                    {
                        // There wasn't a latest stable version of this package. 
                        // Reduce the threshold by this package's download count since it shouldn't be counted.
                        downloadCountThreshold -= (long)(downloadCount * this._downloadPercentage);
                    }

                    Trace.TraceInformation($"Download count {includedDownloadCount.ToString("#,####")} / {downloadCountThreshold.ToString("#,####")}");
                    thresholdReached = (includedDownloadCount >= downloadCountThreshold);

                    if (thresholdReached)
                    {
                        break;
                    }
                }

                Trace.TraceInformation($"Current package count {includedPackagesWithCounts.Count.ToString("#,###")}.");

                currentPosition += batchSize;
            } while (!thresholdReached && batch != null && batch.Count() > 0);

            Trace.TraceInformation($"Including {includedPackagesWithCounts.Count.ToString("#,###")} packages.");

            var sortedIncludedPackages = includedPackagesWithCounts.OrderByDescending(item => item.Item2).ThenBy(item => item.Item1.CatalogEntry.PackageId);
            return sortedIncludedPackages.Select(item => item.Item1);
        }

        IEnumerable<string> StageIdxFiles(IEnumerable<RegistrationIndexPackage> packages, IStorage storage, Version indexerVersion, string outputDirectory)
        {
            List<string> idxFileList = new List<string>();
            Directory.CreateDirectory(outputDirectory);

            foreach (RegistrationIndexPackage package in packages)
            {
                Uri idxResourceUri = storage.ComposeIdxResourceUrl(indexerVersion, package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion);

                using (StorageContent idxContent = storage.Load(idxResourceUri, new CancellationToken()).Result)
                {
                    if (idxContent != null)
                    {
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
                }
            }

            return idxFileList;
        }

        string CreateArdbFile(Version mergerVersion, IEnumerable<string> idxList, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            // Save the idx file list to a file.  
            string idxListFile = Path.Combine(outputDirectory, "IDXList.txt");
            File.WriteAllLines(idxListFile, idxList);

            ElfieCmd cmd = new ElfieCmd(mergerVersion);
            string ardbFile = cmd.RunMerger(idxListFile, outputDirectory);

            return ardbFile;
        }
    }
}

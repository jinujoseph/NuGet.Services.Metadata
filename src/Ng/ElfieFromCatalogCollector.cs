// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using Ng.Elfie;
using Ng.Models;
using Catalog = NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System.Configuration;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis.Elfie.Model;

namespace Ng
{
    /// <summary>
    /// Creates Elfie index, Idx and Ardb, files for NuGet packages.
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
        /// Creates Elfie index, Idx and Ardb, files for NuGet packages.
        /// </summary>
        /// <returns>True if the the indexes are created. False if the indexes
        /// are not created, but the error is transient. If a unrecoverable error 
        /// is encountered an exception is thrown.</returns>
        public async Task<bool> Run(CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity OnProcessBatch");

            try
            {
                // Load the download counts  
                JArray downloadJson = FetchDownloadCounts(this._downloadCountsUri);

                // Get the packages to include in the ardb index
                IList<Tuple<RegistrationIndexPackage, long>> packagesToInclude = GetPackagesToInclude(downloadJson, this._downloadPercentage);
                Trace.TraceInformation($"Including {packagesToInclude.Count} potential NuGet packages.");

                // Get the list of framework assembly packages to include in the ardb index.
                IList<Tuple<RegistrationIndexPackage, long>> assemblyPackagesToInclude = GetAssemblyPackages(Catalog2ElfieOptions.AssemblyPackagesDirectory);
                Trace.TraceInformation($"Including {assemblyPackagesToInclude.Count} potential assembly packages.");

                foreach (var assemblyPackage in assemblyPackagesToInclude)
                {
                    packagesToInclude.Add(assemblyPackage);
                }

                Trace.TraceInformation($"Including {packagesToInclude.Count} total potential packages.");

                // Create the idx index for each package
                await CreateIdxIndexesAsync(packagesToInclude.Select(item => item.Item1), cancellationToken);

                // Create the ardb index
                string outputDirectory = Path.Combine(this._tempPath, Guid.NewGuid().ToString());
                CreateArdbFile(packagesToInclude, this._indexerVersion, this._mergerVersion, outputDirectory);
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
        /// Creates an idx file for each package.
        /// </summary>
        /// <param name="packages">The list of packages to process.</param>
        /// <remarks>If the package's idx file is already in storage, e.g. it was created in
        /// a previous run, we use the stored package. A new idx file is only created for new packages.</remarks>
        async Task CreateIdxIndexesAsync(IEnumerable<RegistrationIndexPackage> packages, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessCatalogItems");

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = this._maxThreads,
            };

            Parallel.ForEach(packages, options, package =>
            {
                Trace.TraceInformation("Processing package {0}", package.CatalogEntry.PackageId);

                // Get the storage url for the idx file. We'll use this to check if the 
                // idx file already exists before going through the effort of creating one.
                Uri idxResourceUri = this._storage.ComposeIdxResourceUrl(this._indexerVersion, package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion);
                StorageContent idxStorageItem = this._storage.Load(idxResourceUri, new CancellationToken()).Result;

                if (idxStorageItem == null)
                {
                    // We didn't have the idx file in storage, so go through the process of downloading,
                    // decompressing and creating the idx file.
                    ProcessPackageDetailsAsync(package, cancellationToken).Wait();
                }
                else
                {
                    Trace.TraceInformation($"Idx already exists in storage. {idxResourceUri}");
                }
            });

            Trace.TraceInformation("#StopActivity ProcessCatalogItems");
        }

        IList<Tuple<RegistrationIndexPackage, long>> GetAssemblyPackages(string assemblyPackageDirectory)
        {
            List<Tuple<RegistrationIndexPackage, long>> assemblyPackageFiles = new List<Tuple<RegistrationIndexPackage, long>>();
            if (Directory.Exists(assemblyPackageDirectory))
            {
                foreach (string file in Directory.GetFiles(assemblyPackageDirectory, "*.txt"))
                {
                    RegistrationIndexPackage package = new RegistrationIndexPackage();
                    package.Id = new Uri(file);
                    package.Type = "AssemblyPackage";
                    RegistrationIndexPackageDetails catalogEntry = new RegistrationIndexPackageDetails();
                    catalogEntry.Id = package.Id;
                    catalogEntry.Type = package.Type;
                    catalogEntry.PackageId = Path.GetFileNameWithoutExtension(file);
                    catalogEntry.PackageVersion = "0.0.0.0";
                    package.CatalogEntry = catalogEntry;

                    // Give the assembly packages the largest download count so they are at the top of the ardb tree.
                    //long downloadCount = Int32.MaxValue;
                    // BUGBUG: Elfie needs to be updated to use longs for the total download count.
                    long downloadCount = (long)Math.Pow(2, 30);

                    assemblyPackageFiles.Add(Tuple.Create((RegistrationIndexPackage)package, downloadCount));
                }
            }

            return assemblyPackageFiles;
        }
        
        /// <summary>
        /// Downloads, decompresses and creates an idx file for a NuGet package.
        /// </summary>
        /// <param name="package">The package to process.</param>
        async Task ProcessPackageDetailsAsync(RegistrationIndexPackage package, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity ProcessPackageDetailsAsync " + package.CatalogEntry.PackageId + " " + package.CatalogEntry.PackageVersion);

            Uri packageResourceUri = null;

            // If this is a NuGet package, download the package
            if (!package.IsLocalPackage)
            {
                packageResourceUri = await this.DownloadPackageAsync(package, cancellationToken);
            }

            // If we successfully downloaded the package or the package is an assembly package, create the idx file for the package.  
            if (packageResourceUri != null || package.IsLocalPackage)
            {
                Uri idxFile = await this.StageAndIndexPackageAsync(packageResourceUri, package, cancellationToken);
            }

            Trace.TraceInformation("#StopActivity ProcessPackageDetailsAsync");
        }

        /// <summary> 
        /// Downloads a package (nupkg) and saves the package to storage. 
        /// </summary> 
        /// <param name="package">The registration data for the package to download.</param> 
        /// <param name="packageDownloadUrl">The download URL for the package to download.</param> 
        /// <returns>The storage resource URL for the saved package.</returns> 
        async Task<Uri> DownloadPackageAsync(RegistrationIndexPackage package, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity DownloadPackageAsync " + package.CatalogEntry.PackageId + " " + package.CatalogEntry.PackageVersion);

            Uri packageResourceUri = null;

            int retryLimit = 3;
            for (int retry = 0; retry < retryLimit; retry++)
            {
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

                    // If something went wrong, we should delete the package from storage so we don't have partially downloaded files. 
                    if (packageResourceUri != null)
                    {
                        try
                        {
                            await this._storage.Delete(packageResourceUri, cancellationToken);
                        }
                        catch
                        {
                        }
                    }

                    // If we received a 404 (file not found) when trying to download the file.   
                    // There's not much we can do here since the package download URL doesn't exist.  
                    // Return null, which indicates that the package doesn't exist, and continue.  
                    WebException webException = e as WebException;
                    if (webException != null && webException.Response != null && ((HttpWebResponse)webException.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        Trace.TraceWarning($"The package download URL returned a 404. {package.PackageContent}");
                        packageResourceUri = null;
                        break;
                    }

                    // For any other exception, retry the download.
                    if (retry < retryLimit - 1)
                    {
                        Trace.TraceWarning($"Exception downloading package on attempt {retry} of {retryLimit - 1}. {e.Message}");

                        // Wait for a few seconds before retrying.
                        int delay = Catalog2ElfieOptions.GetRetryDelay(retry);
                        Thread.Sleep(delay * 1000);

                        Trace.TraceWarning($"Retrying package download.");
                    }
                    else
                    {
                        // We retried a few times and failed. 
                        // Rethrow the exception so we track the failure.
                        throw;
                    }
                }
            }

            Trace.TraceInformation("#StopActivity DownloadPackageAsync");

            return packageResourceUri;
        }

        /// <summary>
        /// Decompresses a nupkg file to a temp directory, runs elfie to create an Idx file for the package, and stores the Idx file.
        /// </summary>
        async Task<Uri> StageAndIndexPackageAsync(Uri packageResourceUri, RegistrationIndexPackage package, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity DecompressAndIndexPackageAsync " + package.CatalogEntry.PackageId + " " + package.CatalogEntry.PackageVersion);

            Uri idxResourceUri = null;

            // This is the temporary directory that we'll work in.
            string tempDirectory = Path.Combine(this._tempPath, Guid.NewGuid().ToString());
            Trace.TraceInformation($"Temp directory: {tempDirectory}.");

            try
            {
                // Create the temp directory and expand the nupkg file
                Directory.CreateDirectory(tempDirectory);

                if (package.IsLocalPackage)
                {
                    // This is an assembly package, so copy the files to the temp directory.
                    this.CopyAssemblyPackage(package.Id.LocalPath, tempDirectory);
                }
                else
                {
                    // This is a NuGet package, so decompress the package.

                    // This is the pointer to the package file in storage
                    Trace.TraceInformation("Loading package from storage.");
                    StorageContent packageStorage = this._storage.Load(packageResourceUri, new CancellationToken()).Result;

                    this.ExpandPackage(packageStorage, tempDirectory);
                }

                string idxFile = this.CreateIdxFile(package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion, tempDirectory);

                if (idxFile == null)
                {
                    Trace.TraceInformation("The idx file was not created.");
                }
                else
                {
                    Trace.TraceInformation($"Saving the idx file. {idxFile}");

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
                    try
                    {
                        await this._storage.Delete(idxResourceUri, cancellationToken);
                        idxResourceUri = null;
                    }
                    catch (Exception e)
                    {
                        // The resource couldn't be deleted.
                        // Log the error and continue. The original exception will be rethrown.
                        Trace.TraceWarning($"Could not delete idx from storage.");
                        Trace.TraceWarning(e.ToString());
                    }
                }

                throw;
            }
            finally
            {
                Trace.TraceInformation("Deleting the temp directory.");
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch
                {
                    // If the temp directory couldn't be deleted just log the error and continue.
                    Trace.TraceWarning($"Could not delete the temp directory {tempDirectory}.");
                }
            }

            Trace.TraceInformation("#StopActivity DecompressAndIndexPackageAsync");

            return idxResourceUri;
        }

        void CopyAssemblyPackage(string assemblyPackageFile, string tempDirectory)
        {
            Trace.TraceInformation("#StartActivity CopyAssemblyPackage");

            string libDirectory = Path.Combine(tempDirectory, "lib");
            Directory.CreateDirectory(libDirectory);

            Trace.TraceInformation($"Copying assemblies to temp directory. {libDirectory}");
            string[] files = File.ReadAllLines(assemblyPackageFile);
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    string fileName = Path.GetFileName(file);
                    string destinationDir = Path.Combine(libDirectory, Guid.NewGuid().ToString());
                    Directory.CreateDirectory(destinationDir);
                    string destinationPath = Path.Combine(destinationDir, fileName);
                    File.Copy(file, destinationPath, true);
                }
                else
                {
                    throw new FileNotFoundException($"Could not find assembly package file {file} defined in {assemblyPackageFile}.");
                }
            }

            Trace.TraceInformation("#StopActivity CopyAssemblyPackage");
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
        /// Downloads the json file which contains the package download counts.
        /// </summary>
        /// <param name="downloadJsonUri">The url to the file which contains the download counts.</param>
        /// <returns>A JArray representing the downloaded file.</returns>
        /// <remarks>The file downloaded is a json array, not a json file. i.e. it is not enclosed in { }.</remarks>
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
            int minimumPackageCount = Catalog2ElfieOptions.MinimumPackageCountFromDownloadUrl;

            Trace.TraceInformation($"Verify download json file package count {downloadJson.Count} > {minimumPackageCount}.");
            if (downloadJson.Count < minimumPackageCount)
            {
                throw new InvalidOperationException($"The download count json file which was downloaded did not contain the minimum set of download data. {downloadJson.Count} < {minimumPackageCount}");
            }

            return downloadJson;
        }

        /// <summary>
        /// Finds all the packages which should be included in the ardb file. 
        /// </summary>
        /// <param name="downloadJson">The json file which contains the download counts for all the of NuGet packages.</param>
        /// <param name="downloadPercentage">The percentage of downloads to include in the ardb file.</param>
        /// <returns></returns>
        IList<Tuple<RegistrationIndexPackage, long>> GetPackagesToInclude(JArray downloadJson, double downloadPercentage)
        {
            long totalDownloadCount;

            // Get the download count for each package
            Dictionary<string, long> packageDownloadCounts = GetDownloadsPerPackage(downloadJson, out totalDownloadCount);

            // Calculate the download threshold
            long downloadCountThreshold = (long)(totalDownloadCount * downloadPercentage);

            // Filter and sort the packages to only the ones we want to include. i.e. remove any packages which do
            // not have a latest stable version and only take packages within the download threshold.
            // Note: packagesToInclude is already sorted in the order the packages should appear in the ardb index.
            IList<Tuple<RegistrationIndexPackage, long>> packagesToInclude = FilterPackagesToInclude(packageDownloadCounts, downloadCountThreshold);

            return packagesToInclude;
        }

        /// <summary>
        ///  Reads the JArray containing the package download counts and converts it to
        ///  a dictionary of package name to download count.
        /// </summary>
        /// <param name="downloadJson">The JArray of package download counts.</param>
        /// <param name="totalDownloadCount">The sum of all the package download counts.</param>
        Dictionary<string, long> GetDownloadsPerPackage(JArray downloadJson, out long totalDownloadCount)
        {
            totalDownloadCount = 0;

            // Get the download count for each package
            Dictionary<string, long> packageDownloadCounts = new Dictionary<string, long>();
            foreach (JArray packageDownloadJson in downloadJson)
            {
                string packageId = packageDownloadJson.First.Value<string>();

                // Sum the downloads for each version of the package.
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

            Trace.TraceInformation($"Total package count from download json: {packageDownloadCounts.Count.ToString("#,###")}");
            Trace.TraceInformation($"Total download count: {totalDownloadCount.ToString("#,###")}");

            return packageDownloadCounts;
        }

        /// <summary>
        /// Gets the latest stable version of a set of packages from the NuGet registration service.
        /// </summary>
        /// <param name="packageIds">The list of package ids.</param>
        /// <returns>A mapping of package ids to its corresponding registration metadata.</returns>
        Dictionary<string, RegistrationIndexPackage> GetLatestStableVersion(IEnumerable<string> packageIds)
        {
            Dictionary<string, RegistrationIndexPackage> latestStableVersions = new Dictionary<string, RegistrationIndexPackage>();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = this._maxThreads;

            // This task is light enough that even on a 1 core machine, we can use 10 threads.
            if (options.MaxDegreeOfParallelism < 10)
            {
                options.MaxDegreeOfParallelism = 10;
            }

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

        /// <summary>
        /// Gets the latest stable version of a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The registration information for the latest stable version of a package.</returns>
        RegistrationIndexPackage GetLatestStableVersion(string packageId)
        {
            Trace.TraceInformation($"Determining latest stable version {packageId}.");

            RegistrationIndexPackage latestStableVersion = null;

            int retryLimit = 3;
            for (int retry = 0; retry < retryLimit; retry++)
            {
                try
                {
                    Uri registrationUri = this._nugetServiceUrls.ComposeRegistrationUrl(packageId);
                    RegistrationIndex registration = null;

                    registration = RegistrationIndex.Deserialize(registrationUri);

                    latestStableVersion = registration.GetLatestStableVersion();
                }
                catch (Exception e) when (e is WebException || e is JsonException)
                {
                    // For 404 errors, return null to indicate that we couldn't get the latest stable version.
                    WebException we = e as WebException;
                    if (we != null &&
                        we.Response != null &&
                        ((HttpWebResponse)we.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        latestStableVersion = null;
                        break;
                    }

                    // For any other WebException or JsonException, retry the operation.
                    if (retry < retryLimit - 1)
                    {
                        Trace.TraceWarning($"Exception getting latest stable version of package on attempt {retry} of {retryLimit - 1}. {e.Message}");

                        // Wait for a few seconds before retrying.
                        int delay = Catalog2ElfieOptions.GetRetryDelay(retry);
                        Thread.Sleep(delay * 1000);

                        Trace.TraceWarning($"Retrying getting latest stable version.");
                    }
                    else
                    {
                        // We've tried a few times and still failed. 
                        // Rethrow the exception so we can track the failure.
                        throw;
                    }
                }
            }

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

        /// <summary>
        /// Filters the package list to only the latest stable version of the packages
        /// </summary>
        /// <param name="packageDownloadCounts"></param>
        /// <param name="downloadCountThreshold"></param>
        /// <returns></returns>
        IList<Tuple<RegistrationIndexPackage, long>> FilterPackagesToInclude(Dictionary<string, long> packageDownloadCounts, long downloadCountThreshold)
        {
            // In general, here's how the filtering works.
            //   1. Calcuate the total download count for all the NuGet packages. (this is done before calling this method.)
            //   2. Calculate how many downloads we want to include in the ardb. (this is done before calling this method.)
            //   3. Exclude any packages which do not have a latest stable version.
            //   4. Sort the packages based on download count.
            //   5. Include the most popular packages until the target download count is reached.
            //   6. Sort the included packages, first by log2(downloadcount), then by package name.
            //      This sort order ensures the most popular packages are recommended first, while
            //      minimizing the day-to-day differences.


            // The runninng count of the downloads included in the ardb.
            long includedDownloadCount = 0;

            // A flag to signal that the download threshold was reached.
            bool thresholdReached = false;
            List<Tuple<RegistrationIndexPackage, long>> includedPackagesWithCounts = new List<Tuple<RegistrationIndexPackage, long>>();

            // We need to determine the latest stable version for each package. But this is a fairly expensive operation since
            // it makes a network call. We'll process the packages in chuncks so we can get the latest stable version of
            // the packages using multiple threads.
            int batchSize = Catalog2ElfieOptions.FilterPackagesToIncludeBatchSize;
            int currentPosition = 0;

            // We need to process the packages in order from most popular to least popular. (item.Value is the download count for the package)
            var orderedDownloadCounts = packageDownloadCounts.OrderByDescending(item => item.Value);
            IEnumerable<string> batch = null;

            do
            {
                // Get the next chunk of packages to process.
                batch = orderedDownloadCounts.Skip(currentPosition).Take(batchSize).Select(item => item.Key);

                // Get the latest stable versions of the packages.
                Dictionary<string, RegistrationIndexPackage> latestStableVersions = GetLatestStableVersion(batch);

                foreach (string packageId in batch)
                {
                    long downloadCount = packageDownloadCounts[packageId];
                    RegistrationIndexPackage latestStableVersion;

                    // If there's a latest stable version for the package, we want to include it.
                    if (latestStableVersions.TryGetValue(packageId, out latestStableVersion))
                    {
                        Trace.TraceInformation($"Included package: {packageId} - {downloadCount.ToString("#,####")}");
                        includedDownloadCount += downloadCount;

                        includedPackagesWithCounts.Add(Tuple.Create((RegistrationIndexPackage)latestStableVersion, downloadCount));
                    }
                    else
                    {
                        // There wasn't a latest stable version of this package. 
                        // Reduce the threshold by this package's download count since it shouldn't be counted.
                        downloadCountThreshold -= (long)(downloadCount * this._downloadPercentage);
                    }

                    // Stop if we've reached the download threhold.
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

            // Basic validation, just check that the package counts are about the right number.
            int minimumPackageCount = Catalog2ElfieOptions.MinimumPackageCountAfterFiltering;

            Trace.TraceInformation($"Verify filtered package count {includedPackagesWithCounts.Count} > {minimumPackageCount}.");
            if (includedPackagesWithCounts.Count < minimumPackageCount)
            {
                throw new InvalidOperationException($"The filtered package count is less than the minimum set of filtered packages. {includedPackagesWithCounts.Count} < {minimumPackageCount}");
            }

            return includedPackagesWithCounts;
        }

        /// <summary>
        /// Stages the files required to create the ardb and run the merger to produce the ardb.
        /// </summary>
        /// <param name="packagesToInclude">The list of packages to include in the ardb.</param>
        /// <param name="indexerVersion">The version of the indexer used to create the idx file.</param>
        /// <param name="mergerVersion">The version of the merger to create the ardb file.</param>
        /// <param name="outputDirectory">The working directory.</param>
        void CreateArdbFile(IList<Tuple<RegistrationIndexPackage, long>> packagesToInclude, Version indexerVersion, Version mergerVersion, string outputDirectory)
        {
            try
            {
                // Set up the directory structure.
                string idxDirectory = Path.Combine(outputDirectory, "idx");
                string logsDirectory = Path.Combine(outputDirectory, "logs");
                Directory.CreateDirectory(outputDirectory);
                Directory.CreateDirectory(idxDirectory);
                Directory.CreateDirectory(logsDirectory);

                // Stage the files and run the merger.
                IEnumerable<string> idxList = StageIdxFiles(packagesToInclude, this._storage, indexerVersion, idxDirectory);
                string ardbTextFile = RunArdbMerger(mergerVersion, idxDirectory, outputDirectory);

                // Save the ardb/txt file.
                string version = DateTime.UtcNow.ToString("yyyyMMdd");
                Uri ardbResourceUri = this._storage.ComposeArdbResourceUrl(mergerVersion, $"{version}\\{version}.ardb.txt");
                this._storage.SaveFileContents(ardbTextFile, ardbResourceUri);
            }
            finally
            {
                try
                {
                    Directory.Delete(outputDirectory, true);
                }
                catch
                {
                    Trace.TraceWarning($"Could not delete the temp directory {outputDirectory}.");
                }
            }
        }

        /// <summary>
        /// Copies the idx files from storage to the local machine. This is to prep the local machine before running the ardb merger.
        /// </summary>
        /// <param name="packagesWithDownloadCounts">The list of packages to copy from storage.</param>
        /// <param name="storage">The storage object that can access the idx files.</param>
        /// <param name="indexerVersion">The indexer version used to create the idx files.</param>
        /// <param name="outputDirectory">The directory to copy the idx files to.</param>
        /// <returns>The local paths to the idx files. This will be used by the ardb merger to identify which packages to include.</returns>
        IEnumerable<string> StageIdxFiles(IList<Tuple<RegistrationIndexPackage, long>> packagesWithDownloadCounts, IStorage storage, Version indexerVersion, string outputDirectory)
        {
            List<string> localIdxFileList = new List<string>();
            Directory.CreateDirectory(outputDirectory);

            // These are the list of packages that are required to be included in the ardb file.
            Dictionary<string, bool> requiredPackagesState = new Dictionary<string, bool>();
            IEnumerable<string> requiredPackages = Catalog2ElfieOptions.RequiredPackages;
            if (requiredPackages == null || requiredPackages.Count() == 0)
            {
                Trace.TraceInformation("The configuration file does not define any required packages to verify.");
            }
            else
            {
                foreach (string part in requiredPackages)
                {
                    requiredPackagesState[part.ToLowerInvariant()] = false;
                }
            }

            foreach (Tuple<RegistrationIndexPackage, long> packageWithDownloadCount in packagesWithDownloadCounts)
            {
                // This is the URL of the idx file in storage. i.e. this is the source path
                Uri idxResourceUri = storage.ComposeIdxResourceUrl(indexerVersion, packageWithDownloadCount.Item1.CatalogEntry.PackageId, packageWithDownloadCount.Item1.CatalogEntry.PackageVersion);

                using (StorageContent idxContent = storage.Load(idxResourceUri, new CancellationToken()).Result)
                {
                    if (idxContent != null)
                    {
                        // This is the path to the local file. i.e. this is the destination path
                        string localFilePath = Path.Combine(outputDirectory, Path.GetFileName(idxResourceUri.LocalPath));
                        localIdxFileList.Add(localFilePath);

                        // Copy the file from storage to the local path
                        using (Stream idxStream = idxContent.GetContentStream())
                        {
                            using (BinaryReader reader = new BinaryReader(idxStream))
                            {
                                // Restamp the idx file with the current download counts.
                                PackageDatabase idxDatabase = new PackageDatabase();
                                idxDatabase.ReadBinary(reader);
                                idxDatabase.Identity.DownloadCount = (int)packageWithDownloadCount.Item2;

                                //  Save the file with the updated download counts.
                                using (FileStream fileStream = File.OpenWrite(localFilePath))
                                {
                                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                                    {
                                        idxDatabase.WriteBinary(writer);
                                        requiredPackagesState[packageWithDownloadCount.Item1.CatalogEntry.PackageId.ToLowerInvariant()] = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Trace.TraceInformation($"Copied {localIdxFileList.Count.ToString("#,###")} idx files from storage to {outputDirectory}");

            // Validate that the we have the idx files for the required packages.
            if (requiredPackages != null && requiredPackages.Count() > 0)
            {
                Trace.TraceInformation($"Verify the {requiredPackages.Count()} required packages are in the ardb.");
            }
            IEnumerable<string> missingPackages = requiredPackagesState.Where(item => item.Value == false).Select(item => item.Key);
            if (missingPackages.Count() > 0)
            {
                throw new InvalidOperationException($"The following required packages do not have idx files: {String.Join(";", missingPackages)}");
            }

            // Basic validation, just check that the package counts are about the right number.
            int minimumPackageCount = Catalog2ElfieOptions.MinimumPackageCountInArdb;

            Trace.TraceInformation($"Verify the ardb package count {localIdxFileList.Count} > {minimumPackageCount}.");
            if (localIdxFileList.Count < minimumPackageCount)
            {
                throw new InvalidOperationException($"The number of idx files to include in the ardb is less than the minimum set of packages. {localIdxFileList.Count} < {minimumPackageCount}");
            }

            return localIdxFileList;
        }

        /// <summary>
        /// Runs the merger to create the ardb file.
        /// </summary>
        /// <param name="mergerVersion">The version of the merger tool to use to create the ardb file.</param>
        /// <param name="idxList">The directory with the idx files to include in the ardb file.</param>
        /// <param name="outputDirectory">The directory to save the ardb file to.</param>
        /// <returns></returns>
        string RunArdbMerger(Version mergerVersion, string idxDirectory, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            ElfieCmd cmd = new ElfieCmd(mergerVersion);
            string ardbFile = cmd.RunMerger(idxDirectory, outputDirectory);

            // Basic validation, just check that the ardb size is about the right number.
            int minimumArdbSize = Catalog2ElfieOptions.MinimumArdbTextSize;

            FileInfo ardbFileInfo = new FileInfo(ardbFile);
            Trace.TraceInformation($"Verify the ardb.txt file size {ardbFileInfo.Length} > {minimumArdbSize}.");
            if (ardbFileInfo.Length < minimumArdbSize)
            {
                throw new InvalidOperationException($"The ardb size was less than the minimum size. {ardbFileInfo.Length} < {minimumArdbSize}");
            }

            return ardbFile;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Elfie.Model;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ng.Elfie;
using Ng.Models;
using Catalog = NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Ng.Sarif;

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
            using (ActivityTimer timer = new ActivityTimer("Run"))
            {
                try
                {
                    // Load the download counts  
                    JArray downloadJson = FetchDownloadCounts(this._downloadCountsUri);

                    // We need to get the list of packages from two different sources. The first source is NuGet
                    // and the second source is from text files in the AssemblyPackages subdirectory.
                    // NuGet
                    //   The NuGet packages are filtered so only the latest stable versions are included.
                    //   The NuGet packages are filtered so only the top XX% of downloads are included.
                    //   The NuGet packages are placed into groups based on their log2(downloadcount). Note: This is done in the elfie merger.
                    // Local Packages
                    //   A fake package is created for each text file in the AssemblyPackages folder. These
                    //     fake packages allow us to include the .NET Framework assemblies in the index.
                    //   The fake packages are placed in the highest grouping.

                    // Get the NuGet packages to include in the ardb index
                    IList<Tuple<RegistrationIndexPackage, long>> packagesToInclude = GetPackagesToInclude(downloadJson, this._downloadPercentage);
                    SarifTraceListener.TraceInformation($"Including {packagesToInclude.Count} potential NuGet packages.");

                    // Get the list of local (framework) assembly packages to include in the ardb index.
                    IList<Tuple<RegistrationIndexPackage, long>> localPackagesToInclude = GetLocalAssemblyPackages(Catalog2ElfieOptions.AssemblyPackagesDirectory);
                    SarifTraceListener.TraceInformation($"Including {localPackagesToInclude.Count} potential local packages.");

                    // Merge the two package lists.
                    foreach (var assemblyPackage in localPackagesToInclude)
                    {
                        packagesToInclude.Add(assemblyPackage);
                    }

                    SarifTraceListener.TraceInformation($"Including {packagesToInclude.Count} total potential packages.");

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

                return true;
            }
        }

        /// <summary>
        /// Creates an idx file for each package.
        /// </summary>
        /// <param name="packages">The list of packages to process.</param>
        /// <remarks>If the package's idx file is already in storage, e.g. it was created in
        /// a previous run, we use the stored package. A new idx file is only created for new packages.</remarks>
        async Task CreateIdxIndexesAsync(IEnumerable<RegistrationIndexPackage> packages, CancellationToken cancellationToken)
        {
            using (ActivityTimer timer = new ActivityTimer("ProcessCatalogItems"))
            {
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
                        SarifTraceListener.TraceInformation($"Idx already exists in storage for package {package.CatalogEntry.PackageId}.");
                    }
                });
            }
        }

        /// <summary>
        /// Gets the list of fake packages which represent assemblies on the local machine.
        /// </summary>
        /// <param name="assemblyPackageDirectory">The directory which contain the text files representing the local packages.</param>
        /// <returns>Returns faked package registration objects which contain the information for the local packages.</returns>
        IList<Tuple<RegistrationIndexPackage, long>> GetLocalAssemblyPackages(string assemblyPackageDirectory)
        {
            List<Tuple<RegistrationIndexPackage, long>> assemblyPackageFiles = new List<Tuple<RegistrationIndexPackage, long>>();

            if (Directory.Exists(assemblyPackageDirectory))
            {
                foreach (string file in Directory.GetFiles(assemblyPackageDirectory, "*.txt"))
                {
                    // Create the fake registration data.
                    RegistrationIndexPackage package = new RegistrationIndexPackage();
                    package.Id = new Uri(file);
                    package.Type = "AssemblyPackage";
                    RegistrationIndexPackageDetails catalogEntry = new RegistrationIndexPackageDetails();
                    catalogEntry.Id = package.Id;
                    catalogEntry.Type = package.Type;
                    // The package id is the name of the text file.
                    catalogEntry.PackageId = Path.GetFileNameWithoutExtension(file);
                    // The package version is always 0.0.0.0.
                    catalogEntry.PackageVersion = "0.0.0.0";
                    package.CatalogEntry = catalogEntry;

                    // Give the assembly packages the largest download count so they are at the top of the ardb tree.
                    long downloadCount = Int32.MaxValue;

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

            Uri idxFile = null;
            Uri packageResourceUri = null;

            // If this is a NuGet package, download the package
            if (!package.IsLocalPackage)
            {
                packageResourceUri = await this.DownloadPackageAsync(package, cancellationToken);

                if (packageResourceUri == null)
                {
                    SarifTraceListener.TraceWarning("NG005", $"Could not download package {package.CatalogEntry.PackageId}");
                }
            }

            // If we successfully downloaded the package or the package is an local package, create the idx file for the package.  
            if (packageResourceUri != null || package.IsLocalPackage)
            {
                idxFile = await this.StageAndIndexPackageAsync(packageResourceUri, package, cancellationToken);

                if (idxFile == null)
                {
                    SarifTraceListener.TraceWarning("NG006", $"Could not create idx file for package {package.CatalogEntry.PackageId}");
                }
                else
                {
                    SarifTraceListener.TraceInformation($"Created Idx file for package {package.CatalogEntry.PackageId}.");
                }
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
                        SarifTraceListener.TraceWarning("NG011", $"The package download URL for the package {package.CatalogEntry.PackageId} could not be found (404). {package.PackageContent}", webException);
                        packageResourceUri = null;
                        break;
                    }

                    // For any other exception, retry the download.
                    if (retry < retryLimit - 1)
                    {
                        SarifTraceListener.TraceWarning($"Exception downloading package for {package.CatalogEntry.PackageId} on attempt {retry} of {retryLimit - 1}. {e.Message}");

                        // Wait for a few seconds before retrying.
                        int delay = Catalog2ElfieOptions.GetRetryDelay(retry);
                        Thread.Sleep(delay * 1000);

                        SarifTraceListener.TraceWarning($"Retrying package download for {package.CatalogEntry.PackageId}.");
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
            Trace.TraceInformation("#StartActivity StageAndIndexPackageAsync " + package.CatalogEntry.PackageId + " " + package.CatalogEntry.PackageVersion);

            Uri idxResourceUri = null;
            bool includeFrameworkTargets = true;

            // This is the temporary directory that we'll work in.
            string tempDirectory = Path.Combine(this._tempPath, Guid.NewGuid().ToString());
            Trace.TraceInformation($"Temp directory: {tempDirectory}.");

            try
            {
                // Create the temp directory and expand the nupkg file
                Directory.CreateDirectory(tempDirectory);

                if (package.IsLocalPackage)
                {
                    // This is a local package, so copy just the assemblies listed in the text file to the temp directory.
                    this.StageLocalPackageContents(package.Id.LocalPath, tempDirectory);

                    // Do not include the package framework targets for the local packages (becuase the local packages do not define
                    // a target framework.)
                    includeFrameworkTargets = false;
                }
                else
                {
                    // This is a NuGet package, so decompress the package.

                    // This is the pointer to the package file in storage
                    Trace.TraceInformation("Loading package from storage.");
                    StorageContent packageStorage = this._storage.Load(packageResourceUri, new CancellationToken()).Result;

                    // Expand the package contents to the temp directory.
                    this.ExpandPackage(packageStorage, tempDirectory);
                }

                string idxFile = this.CreateIdxFile(package.CatalogEntry.PackageId, package.CatalogEntry.PackageVersion, tempDirectory, includeFrameworkTargets);

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
                SarifTraceListener.TraceWarning("NG007", $"Could not decompress the package. {packageResourceUri}", ze);
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
                        SarifTraceListener.TraceWarning("NG008", $"Could not delete the idx file from storage.", e);
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
                catch (Exception e)
                {
                    // If the temp directory couldn't be deleted just log the error and continue.
                    SarifTraceListener.TraceWarning("NG009", $"Could not delete the temp directory {tempDirectory}.", e);
                }
            }

            Trace.TraceInformation("#StopActivity StageAndIndexPackageAsync");

            return idxResourceUri;
        }

        /// <summary>
        /// Copies the contents of a local package to the temp directory.
        /// </summary>
        /// <param name="localPackageFile">The local package.</param>
        /// <param name="tempDirectory">The destination directory to copy the package contents to.</param>
        void StageLocalPackageContents(string localPackageFile, string tempDirectory)
        {
            Trace.TraceInformation("#StartActivity StageLocalPackageContents");

            // The assemblies need to reside in a lib subdirectory. This is where the assembly
            // references are in a real package.
            string libDirectory = Path.Combine(tempDirectory, "lib");
            Directory.CreateDirectory(libDirectory);

            // Copy the assemblies listed in the text file. Note that we're essentially assuming that
            // the assembly paths are fully qualified.
            Trace.TraceInformation($"Copying assemblies to temp directory. {libDirectory}");
            string[] files = File.ReadAllLines(localPackageFile);
            foreach (string file in files)
            {
                if (!String.IsNullOrWhiteSpace(file))
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
                        throw new FileNotFoundException($"Could not find assembly package file {file} defined in {localPackageFile}.");
                    }
                }
            }

            Trace.TraceInformation("#StopActivity StageLocalPackageContents");
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
        string CreateIdxFile(string packageId, string packageVersion, string tempDirectory, bool includeFrameworkTargets)
        {
            Trace.TraceInformation("#StartActivity CreateIdxFile " + packageId + " " + packageVersion);

            Trace.TraceInformation("Creating the idx file.");

            ElfieCmd cmd = new ElfieCmd(this._indexerVersion);
            string idxFile = cmd.RunIndexer(tempDirectory, packageId, packageVersion, includeFrameworkTargets);

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
            using (ActivityTimer timer = new ActivityTimer("FetchDownloadCounts"))
            {
                JArray downloadJson;
                using (WebClient webClient = new WebClient())
                {
                    string downloadText = webClient.DownloadString(downloadJsonUri);
                    downloadJson = JArray.Parse(downloadText);
                }

                SarifTraceListener.TraceInformation($"Total packages in download json: {downloadJson.Count.ToString("#,###")}");
                SarifTraceListener.TraceInformation("NG910", downloadJson.Count.ToString());

                // Basic validation, just check that the package counts are about the right number.
                int minimumPackageCount = Catalog2ElfieOptions.MinimumPackageCountFromDownloadUrl;

                Trace.TraceInformation($"Verify download json file package count {downloadJson.Count} > {minimumPackageCount}.");
                if (downloadJson.Count < minimumPackageCount)
                {
                    throw new InvalidOperationException($"The download count json file which was downloaded did not contain the minimum set of download data. {downloadJson.Count} < {minimumPackageCount}");
                }

                return downloadJson;
            }
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

            SarifTraceListener.TraceInformation($"Total download count: {totalDownloadCount.ToString("#,###")}.");
            SarifTraceListener.TraceInformation($"Download count threshold: {downloadCountThreshold.ToString("#,###")}.");

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
        /// If Latest version is not found it picks the latest prerelease version.
        /// </summary>
        /// <param name="packageIds">The list of package ids.</param>
        /// <returns>A mapping of package ids to its corresponding registration metadata.</returns>
        Dictionary<string, RegistrationIndexPackage> GetLatestVersion(IEnumerable<string> packageIds)
        {
            Dictionary<string, RegistrationIndexPackage> latestVersions = new Dictionary<string, RegistrationIndexPackage>();

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
                    lock (latestVersions)
                    {
                        latestVersions[packageId] = package;
                    }
                }
                else
                {
                    package = this.GetLatestPreReleaseVersion(packageId);
                    if (package != null)
                    {
                        lock (latestVersions)
                        {
                            latestVersions[packageId] = package;
                        }
                    }
                }
            });

            return latestVersions;
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
                        SarifTraceListener.TraceWarning("NG010", $"The registration URL for the package {packageId} could not be found (404).", we);
                        latestStableVersion = null;
                        break;
                    }

                    // For any other WebException or JsonException, retry the operation.
                    if (retry < retryLimit - 1)
                    {
                        SarifTraceListener.TraceWarning($"Exception getting latest stable version of package on attempt {retry} of {retryLimit - 1}. {e.Message}");

                        // Wait for a few seconds before retrying.
                        int delay = Catalog2ElfieOptions.GetRetryDelay(retry);
                        Thread.Sleep(delay * 1000);

                        SarifTraceListener.TraceWarning($"Retrying getting latest stable version.");
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
                SarifTraceListener.TraceWarning("NG003", $"No latest stable version for package {packageId}.");
            }
            else
            {
                Trace.TraceInformation($"Latest stable version {packageId} {latestStableVersion.CatalogEntry.PackageVersion}.");
            }

            return latestStableVersion;
        }

        /// <summary>
        /// Gets the latest prerelease version of a package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The registration information for the latest prerelease version of a package.</returns>
        RegistrationIndexPackage GetLatestPreReleaseVersion(string packageId)
        {
            Trace.TraceInformation($"Determining latest prerelease version {packageId}.");

            RegistrationIndexPackage latestPreReleaseVersion = null;

            int retryLimit = 3;
            for (int retry = 0; retry < retryLimit; retry++)
            {
                try
                {
                    Uri registrationUri = this._nugetServiceUrls.ComposeRegistrationUrl(packageId);
                    RegistrationIndex registration = null;

                    registration = RegistrationIndex.Deserialize(registrationUri);

                    latestPreReleaseVersion = registration.GetLatestPreReleaseVersion();
                }
                catch (Exception e) when (e is WebException || e is JsonException)
                {
                    // For 404 errors, return null to indicate that we couldn't get the latest prerelease version.
                    WebException we = e as WebException;
                    if (we != null &&
                        we.Response != null &&
                        ((HttpWebResponse)we.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        SarifTraceListener.TraceWarning("NG010", $"The registration URL for the package {packageId} could not be found (404).", we);
                        latestPreReleaseVersion = null;
                        break;
                    }

                    // For any other WebException or JsonException, retry the operation.
                    if (retry < retryLimit - 1)
                    {
                        SarifTraceListener.TraceWarning($"Exception getting latest prerelease version of package on attempt {retry} of {retryLimit - 1}. {e.Message}");

                        // Wait for a few seconds before retrying.
                        int delay = Catalog2ElfieOptions.GetRetryDelay(retry);
                        Thread.Sleep(delay * 1000);

                        SarifTraceListener.TraceWarning($"Retrying getting latest prerelease version.");
                    }
                    else
                    {
                        // We've tried a few times and still failed. 
                        // Rethrow the exception so we can track the failure.
                        throw;
                    }
                }
            }

            if (latestPreReleaseVersion == null)
            {
                SarifTraceListener.TraceWarning("NG104", $"No latest prerelease version for package {packageId}.");
            }
            else
            {
                Trace.TraceInformation($"Latest prerelease version {packageId} {latestPreReleaseVersion.CatalogEntry.PackageVersion}.");
            }

            return latestPreReleaseVersion;
        }

        /// <summary>
        /// Filters the package list to only the latest stable version of the packages
        /// </summary>
        /// <param name="packageDownloadCounts"></param>
        /// <param name="downloadCountThreshold"></param>
        /// <returns></returns>
        IList<Tuple<RegistrationIndexPackage, long>> FilterPackagesToInclude(Dictionary<string, long> packageDownloadCounts, long downloadCountThreshold)
        {
            using (ActivityTimer timer = new ActivityTimer("FilterPackagesToInclude"))
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

                    // Get the latest versions of the packages.
                    Dictionary<string, RegistrationIndexPackage> latestVersions = GetLatestVersion(batch);

                    foreach (string packageId in batch)
                    {
                        long downloadCount = packageDownloadCounts[packageId];
                        RegistrationIndexPackage latestStableVersion;

                        // If there's a latest stable version for the package, we want to include it.
                        if (latestVersions.TryGetValue(packageId, out latestStableVersion))
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

                SarifTraceListener.TraceInformation($"Including {includedPackagesWithCounts.Count.ToString("#,###")} packages.");
                SarifTraceListener.TraceInformation("NG911", includedPackagesWithCounts.Count.ToString());

                // Basic validation, just check that the package counts are about the right number.
                int minimumPackageCount = Catalog2ElfieOptions.MinimumPackageCountAfterFiltering;

                Trace.TraceInformation($"Verify filtered package count {includedPackagesWithCounts.Count} > {minimumPackageCount}.");
                if (includedPackagesWithCounts.Count < minimumPackageCount)
                {
                    throw new InvalidOperationException($"The filtered package count is less than the minimum set of filtered packages. {includedPackagesWithCounts.Count} < {minimumPackageCount}");
                }

                return includedPackagesWithCounts;
            }
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
            using (ActivityTimer timer = new ActivityTimer("CreateArdbFile"))
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

                    SarifTraceListener.TraceInformation($"Saved ardb/txt file to {ardbResourceUri}.");

                    // Save the ardb/txt file to latest.txt. This is the file consumed by the publisher.  
                    Uri latestResourceUri = this._storage.ComposeArdbResourceUrl(mergerVersion, $"latest\\latest.txt");
                    this._storage.SaveFileContents(ardbTextFile, latestResourceUri);

                    SarifTraceListener.TraceInformation($"Saved ardb/txt file to {latestResourceUri}.");
                }
                finally
                {
                    try
                    {
                        Directory.Delete(outputDirectory, true);
                    }
                    catch (Exception e)
                    {
                        SarifTraceListener.TraceWarning("NG009", $"Could not delete the temp directory {outputDirectory}.", e);
                    }
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

            SarifTraceListener.TraceInformation($"Verify the ardb package count {localIdxFileList.Count} > {minimumPackageCount}.");
            SarifTraceListener.TraceInformation("NG913", localIdxFileList.Count.ToString());
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
            SarifTraceListener.TraceInformation($"Verify the ardb.txt file size {ardbFileInfo.Length} > {minimumArdbSize}.");
            SarifTraceListener.TraceInformation("NG912", ardbFileInfo.Length.ToString());
            if (ardbFileInfo.Length < minimumArdbSize)
            {
                throw new InvalidOperationException($"The ardb size was less than the minimum size. {ardbFileInfo.Length} < {minimumArdbSize}");
            }

            return ardbFile;
        }
    }
}

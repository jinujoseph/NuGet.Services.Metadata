// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng
{
    public class Catalog2Elfie
    {
        async Task CreateIndex(Catalog2ElfieOptions options, CancellationToken cancellationToken)
        {
            // The NuGet service catalog root.
            Uri nugetServicesUri = new Uri(options.Source);

            // The list of NuGet service endpoints.
            NugetServiceEndpoints nugetServiceUrls = new NugetServiceEndpoints(new Uri(options.Source));

            Storage storage = options.StorageFactory.Create();

            // Store the package catalog to a version directory. The package catalog is the list of the latest stable version of every package.
            Uri packageCatalogUri = storage.ComposeIdxResourceUrl(options.IndexerVersion, "packagecatalog.json");
            Ng.Models.PackageCatalog packageCatalog = new Ng.Models.PackageCatalog(nugetServicesUri, storage, packageCatalogUri, nugetServiceUrls);
            await packageCatalog.LoadAsync(packageCatalogUri, storage, cancellationToken);

            ElfieFromCatalogCollector collector = new ElfieFromCatalogCollector(options.IndexerVersion, options.MergerVersion, nugetServiceUrls, storage, options.MaxThreads, options.TempPath, packageCatalog);
            await collector.Run(cancellationToken);
            await packageCatalog.SaveAsync(cancellationToken);
        }

        void PrintUsage()
        {
            Console.WriteLine("Creates Elfie index (idx) files for NuGet packages.");
            Console.WriteLine();
            Console.WriteLine("Usage: ng.exe catalog2elfie -indexerVersion <version> -mergerVersion <version> -source <catalog> -downloadSource <source> -downloadPercentage -storageType file|azure -storageBaseAddress <storage-base-address> [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] [-verbose true|false] [-maxThreads <int>] [-tempPath <path>]");
            Console.WriteLine();
            Console.WriteLine("    -indexerVersion      The version of the Elfie indexer to use to create the idx files.");
            Console.WriteLine("    -indexerVersion      The version of the Elfie merger to use to create the idx files.");
            Console.WriteLine("    -source              The NuGet service source URL. e.g. http://api.nuget.org/v3/index.json");
            Console.WriteLine("    -downloadSource      The NuGet package download json URL. e.g. https://nugetprod0.blob.core.windows.net/ng-search-data/downloads.v1.json");
            Console.WriteLine("    -downloadPercentage  The percentage of the total download count to include in the ardb file. e.g. 0.95");
            Console.WriteLine("    -storageType         file|azure Where to store the idx files. Azure will save to blob storage. File will save to the local file system.");
            Console.WriteLine("    -storageBaseAddress  The URL which corresponds to the storage root. For Azure, this is the blob storage URL. For File, this is the file:// url to the local file system.");
            Console.WriteLine("    -storagePath         When storageType=file, the local file path to save the idx files to. e.g. C:\\NuGet\\Crawler");
            Console.WriteLine("                         When storageType=azure, the relative path within the container to save the idx files to. e.g. NuGet/Crawler");
            Console.WriteLine("    -storageAccountName  The Azure storage account name. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -storageKeyValue     The Azure storage key. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -storageContainer    The Azure storage container. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -verbose             true|false Writes trace statements to the console. The default value is false. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -interval            The number of seconds to wait between querying for new or updated packages. The default value is 3 seconds.");
            Console.WriteLine("    -maxThreads          The maximum number of threads to use for processing. The default value is the number of processors.");
            Console.WriteLine("    -tempPath            The working directory to use when saving temporary files to disk. The default value is the system temporary folder.");
            Console.WriteLine();
            Console.WriteLine("Example: ng.exe catalog2elfie -mergerVersion 1.0.0.0 -source http://api.nuget.org/v3/index.json -downloadSource https://nugetprod0.blob.core.windows.net/ng-search-data/downloads.v1.json -downloadPercentage 0.95 -storageBaseAddress file:///C:/NuGet -storageType file -storagePath C:\\NuGet -verbose true");
        }

        public void Run(string[] args, CancellationToken cancellationToken)
        {
            Catalog2ElfieOptions options;

            try
            {
                // Parse the command line arguments
                options = Catalog2ElfieOptions.FromArgs(args);
            }
            catch (Exception e)
            {
                // If the command line arguments were invalid, print help.
                Console.WriteLine(e.Message);

                AggregateException aggregrateException = e as AggregateException;
                if (aggregrateException != null)
                {
                    foreach (Exception innerException in aggregrateException.InnerExceptions)
                    {
                        Console.WriteLine(innerException.Message);
                    }
                }

                Trace.TraceError(e.ToString());

                Console.WriteLine();
                PrintUsage();
                return;
            }

            if (options.Verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
            }

            Trace.TraceInformation("Catalog2Elfie Options: " + Environment.NewLine + options.ToText());

            CreateIndex(options, cancellationToken).Wait();
        }
    }
}

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
        async Task Loop(Catalog2ElfieOptions options, CancellationToken cancellationToken)
        {
            Func<HttpMessageHandler> handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(options.Verbose, null, null);

            Storage storage = options.StorageFactory.Create();
            ReadWriteCursor front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.Min.Value);
            ReadCursor back = MemoryCursor.Max;

            CommitCollector collector = new ElfieFromCatalogCollector(new Uri(options.Source), storage, options.MaxThreads, handlerFunc);

            while (true)
            {
                // Keep running as long as there are more packages to process.
                bool run = false;
                do
                {
                    run = await collector.Run(front, back, cancellationToken);
                }
                while (run);

                // When there are no more packages to process, either stop the application or 
                // wait a few seconds before checking for new packages.
                if (options.Interval <= 0)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(options.Interval * 1000);
                }
            }
        }

        void PrintUsage()
        {
            Console.WriteLine("Creates Elfie index (idx) files for NuGet packages.");
            Console.WriteLine();
            Console.WriteLine("Usage: ng.exe catalog2elfie -source <catalog> -storageType file|azure -storageBaseAddress <storage-base-address> [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] [-verbose true|false] [-interval <seconds>] [-maxthreads <int>]");
            Console.WriteLine();
            Console.WriteLine("    -source              The NuGet catalog source URL. e.g. http://api.nuget.org/v3/catalog0/index.json");
            Console.WriteLine("    -storageType         file|azure Where to store the idx files. Azure will save to blob storage. File will save to the local file system.");
            Console.WriteLine("    -storageBaseAddress  The URL which corresponds to the storage root. For Azure, this is the blob storage URL. For File, this is the file:// url to the local file system.");
            Console.WriteLine("    -storagePath         When storageType=file, the local file path to save the idx files to. e.g. C:\\NuGet\\Crawler");
            Console.WriteLine("                         When storageType=azure, the relative path within the container to save the idx files to. e.g. NuGet/Crawler");
            Console.WriteLine("    -storageAccountName  The Azure storage account name. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -storageKeyValue     The Azure storage key. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -storageContainer    The Azure storage container. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -verbose             true|false Writes trace statements to the console. The default value is false. This parameter is only used when storageType=azure.");
            Console.WriteLine("    -interval            The number of seconds to wait between querying for new or updated packages. The default value is 3 seconds.");
            Console.WriteLine("    -maxthreads          The maximum number of threads to use for processing. The default value is the number of processors.");
            Console.WriteLine();
            Console.WriteLine("Example: ng.exe catalog2elfie -source http://api.nuget.org/v3/catalog0/index.json -storageBaseAddress file:///C:/NuGet -storageType file -storagePath C:\\NuGet -verbose true");
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

                PrintUsage();
                return;
            }

            if (options.Verbose)
            {
                Trace.Listeners.Add(new Ng.TraceListeners.ConsoleTraceListener());
            }

            Trace.TraceInformation("Catalog2Elfie Options: " + options.ToText());

            Loop(options, cancellationToken).Wait();
        }
    }
}

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
            Console.WriteLine();
            Console.WriteLine("Usage: ng catalog2elfie -source <catalog> -storageBaseAddress <storage-base-address> -storageType file|azure [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] [-verbose true|false] [-interval <seconds>] [-maxthreads <int>]");
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
                Trace.Listeners.Add(new ConsoleTraceListener());
            }

            Trace.TraceInformation("Catalog2Elfie Options: " + options.ToText());

            Loop(options, cancellationToken).Wait();
        }
    }
}

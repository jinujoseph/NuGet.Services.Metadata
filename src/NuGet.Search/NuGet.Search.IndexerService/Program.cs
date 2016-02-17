using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Search.Service;

namespace NuGet.Search.IndexerService
{
    class Program
    {
        private static ConcurrentQueue<Config> s_logQueue = new ConcurrentQueue<Config>();

        static int Main()
        {
            ServiceHelper.BeginService();
            Config config = new Config();

            try
            {
                if (!config.Initialize())
                {
                    config.ShowUsage();
                    return 1;
                }

                Trace.TraceInformation("Read configuration values.");

                for (int i = 0; i < config.MaxIndexerThreads; i++)
                {
                    Thread thread = new Thread(new ThreadStart(IndexThread));
                    thread.IsBackground = true;
                    thread.Start();
                }

                if (Environment.UserInteractive)
                {
                    // We're running from the command line.

                    try
                    {
                        CrawlLogs(config);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Exception crawling the Azure storage table logs. Waiting for the queue to drain. \r\n" + e.ToString());
                    }

                    // Wait for the queue to start.
                    Thread.Sleep(5000);

                    // Wait for the queue to be empty before exiting.
                    while (s_logQueue.Count > 0)
                    {
                        Trace.TraceInformation("Waiting for queue to drain. {0} items left.", s_logQueue.Count);
                        Thread.Sleep(5000);
                    }
                }
                else
                {
                    // We're running as a service.

                    // Start worker threads.
                    Thread thread = new Thread(CrawlLogsThread);
                    thread.IsBackground = true;
                    thread.Start(config);

                    Trace.TraceInformation("Spawned worker threads.");

                    // Sleep forever so that asynchronous operations happen.
                    ServiceHelper.ShutdownEvent.WaitOne();
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Exception in main thread!\n{0}\n", e.ToString());
            }
            finally
            {
                if (!String.IsNullOrWhiteSpace(config.OutputDirectory))
                {
                    Directory.CreateDirectory(config.OutputDirectory);
                }
            }

            ServiceHelper.EndService();

            return 0;
        }

        private static void CrawlLogsThread(object configObj)
        {
            Trace.TraceInformation("#StartActivity CrawlLogsThread");

            Config config = (Config)configObj;

            try
            {
                while (true)
                {
                    try
                    {
                        CrawlLogs(config);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceWarning("Exception in CrawlLogsThread!\n{0}", e.ToString());
                    }
                    finally
                    {
                        Trace.TraceInformation("Sleeping for 1 hour before next crawl.");
                        Thread.Sleep(1000 * 60 * 60 * 1);
                    }
                }
            }
            finally
            {
                Trace.TraceInformation("#StopActivity");
            }
        }

        private static void CrawlLogs(Config config)
        {
            //V2FeedContext context = new V2FeedContext(new Uri(config.PackageSource));
            //context.IgnoreMissingProperties = true;
            //context.IgnoreResourceNotFoundException = true;

            //Trace.TraceInformation("Querying for packages.");

            //int packageCount = context.Packages.Count();
            //Trace.TraceInformation("Queueing {0} total packages.", packageCount);

            //// Each query is limited to 100 results.
            //int pageSize = 100;
            //for (int i = 0; i < packageCount; i = i + pageSize)
            //{
            //    var packages = context.Packages.Skip(i).Take(pageSize);

            //    foreach (V2FeedPackage p in packages)
            //    {
            //        if (p != null)
            //        {
            //            Trace.TraceInformation("Processing package {0}-{1}", p.Id, p.Version.ToString());

            //            if (p.IsLatestVersion)
            //            {
            //                if (!config.IndexLatestVersion)
            //                {
            //                    Trace.TraceInformation("Skipping package {0}-{1}. Package is latest version.", p.Id, p.Version.ToString());
            //                    continue;
            //                }
            //            }
            //            else if (p.IsAbsoluteLatestVersion)
            //            {
            //                if (!config.IndexAbsoluteLatestVersion)
            //                {
            //                    Trace.TraceInformation("Skipping package {0}-{1}. Package is absolute latest version", p.Id, p.Version.ToString());
            //                    continue;
            //                }
            //            }
            //            else if (!config.IndexHistoricalVersions)
            //            {
            //                Trace.TraceInformation("Skipping package {0}-{1}. Package is historical version.", p.Id, p.Version.ToString());
            //                continue;
            //            }

            //            Trace.TraceInformation("Creating config file for package {0}-{1}.", p.Id, p.Version.ToString());
            //            Config tempConfig = new Config();
            //            tempConfig.ElasticSearchServerUrl = config.ElasticSearchServerUrl;
            //            tempConfig.IndexName = config.IndexName;
            //            tempConfig.PackageSource = config.PackageSource;
            //            tempConfig.IndexLatestVersion = config.IndexLatestVersion;
            //            tempConfig.IndexAbsoluteLatestVersion = config.IndexAbsoluteLatestVersion;
            //            tempConfig.IndexHistoricalVersions = config.IndexHistoricalVersions;
            //            tempConfig.PackageId = p.Id;
            //            tempConfig.PackageVersion = p.Version;

            //            Trace.TraceInformation("Queueing config file for package {0}-{1}.", p.Id, p.Version.ToString());
            //            s_packageQueue.Enqueue(tempConfig);
            //        }
            //    }
            //}

            //Trace.TraceInformation("End crawling.");
        }

        private static void IndexThread()
        {
            Trace.TraceInformation("#Starting thread IndexThread");

            while (true)
            {
                Trace.TraceInformation("Current queue length is {0}", s_logQueue.Count);

                Config config;
                if (s_logQueue.TryDequeue(out config))
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Trace.TraceInformation("#StartActivity IndexThread");

                    try
                    {
                    }
                    finally
                    {
                    }

                    stopwatch.Stop();
                    Trace.TraceInformation("#EndActivity " + stopwatch.ElapsedMilliseconds + " ms");
                }
                else
                {
                    Trace.TraceInformation("No work in queue. Sleeping for 1 second.");
                    Thread.Sleep(1000);
                }
            }
        }
    }
}


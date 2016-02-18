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
using NuGet.Search.Common.ElasticSearch.Sarif;
using NuGet.Search.Common.ElasticSearch;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Ng.TraceListeners.Models;
using Nest;

namespace NuGet.Search.IndexerService
{
    class Program
    {
        ConcurrentQueue<ResultLog> _logQueue = new ConcurrentQueue<ResultLog>();
        Options _options;

        static int Main(string[] args)
        {
            ServiceHelper.BeginService();
            Options options = new Options();

            try
            {
                options.ReadAppConfigValues();

                if (!CommandLine.Parser.Default.ParseArguments(args, options))
                {
                    return 1;
                }

                if (!options.Validate())
                {
                    Trace.TraceInformation(options.GetUsage());
                    return 1;
                }

                Trace.TraceInformation(options.GetParameterValueText());

                Program program = new Program();
                program.Run(options);
            }
            catch (Exception e)
            {
                Trace.TraceError("Exception in main thread!\n{0}\n", e.ToString());
            }

            ServiceHelper.EndService();

            return 0;
        }

        void Run(Options options)
        {
            this._options = options;

            for (int i = 0; i < this._options.MaxIndexerThreads; i++)
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
                    CrawlLogs();
                }
                catch (Exception e)
                {
                    Trace.TraceError("Exception crawling the Azure storage table logs. Waiting for the queue to drain. \r\n" + e.ToString());
                }

                // Wait for the queue to start.
                Thread.Sleep(500);

                // Wait for the queue to be empty before exiting.
                while (_logQueue.Count > 0)
                {
                    Trace.TraceInformation("Waiting for queue to drain. {0} items left.", _logQueue.Count);
                    Thread.Sleep(5000);
                }
            }
            else
            {
                // We're running as a service.

                // Start worker threads.
                Thread thread = new Thread(CrawlLogsThread);
                thread.IsBackground = true;
                thread.Start();

                Trace.TraceInformation("Spawned worker threads.");

                // Sleep forever so that asynchronous operations happen.
                ServiceHelper.ShutdownEvent.WaitOne();
            }
        }

        private void CrawlLogsThread()
        {
            Trace.TraceInformation("#StartActivity CrawlLogsThread");

            try
            {
                while (true)
                {
                    try
                    {
                        CrawlLogs();
                    }
                    catch (Exception e)
                    {
                        Trace.TraceWarning("Exception in CrawlLogsThread!\n{0}", e.ToString());
                    }
                    finally
                    {
                        Trace.TraceInformation("Sleeping for 1 hour before next crawl.");
                        //Thread.Sleep(1000 * 60 * 60 * 1);
                        Thread.Sleep(1000 * 5);
                    }
                }
            }
            finally
            {
                Trace.TraceInformation("#StopActivity");
            }
        }

        private void CrawlLogs()
        {
            ISarifProvider provider = new AzureStorageTableSarifProvider(this._options.AzureStorageConnectionString, this._options.AzureStorageTableName, this._options.ElasticSearchServerUrl, this._options.IndexName);

            IEnumerable<ResultLog> logs = provider.GetNextBatch();
            Trace.TraceInformation($"Queueing {logs.Count()} results.");

            foreach (ResultLog log in logs)
            {
                _logQueue.Enqueue(log);
            }
        }

        private void IndexThread()
        {
            Trace.TraceInformation("#Starting thread IndexThread");

            while (true)
            {
                Trace.TraceInformation("Current queue length is {0}", _logQueue.Count);

                ResultLog resultLog;
                if (_logQueue.TryDequeue(out resultLog))
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Trace.TraceInformation($"#StartActivity IndexThread for {resultLog.RunLogs[0].RunInfo.InvocationInfo}");

                    try
                    {
                        ElasticSearchClient client = new ElasticSearchClient(this._options.ElasticSearchServerUrl, this._options.IndexName);

                        ResultLog existingLog = client.GetDocument<ResultLog>(resultLog.Id);

                        if (existingLog == null)
                        {
                            existingLog = resultLog;
                        }
                        else
                        {
                            List<Result> results = new List<Result>(existingLog.RunLogs[0].Results);
                            foreach (Result result in resultLog.RunLogs[0].Results)
                            {
                                if (!results.Exists(r => r.Properties["RowKey"] == result.Properties["RowKey"]))
                                {
                                    results.Add(result);
                                }
                            }

                            existingLog.RunLogs[0].Results = results.OrderBy(r => r.Properties["RowKey"]).ToList();
                        }

                        client.Index<ResultLog>(existingLog);
                        client.Client.Refresh(new RefreshRequest());
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


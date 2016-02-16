﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System.Text;
using Ng.Elfie;
using System.Configuration;

namespace Ng
{
    class Catalog2ElfieOptions
    {
        static int? s_retryDelayInSeconds = null;
        static int? s_filterPackagesToIncludeBatchSize = null;
        static int? s_minimumPackageCountFromDownloadUrl = null;
        static int? s_minimumPackageCountAfterFiltering = null;
        static int? s_minimumPackageCountInArdb = null;
        static int? s_minimumArdbTextSize = null;
        static List<String> s_requiredPackages = null;
        static string s_assemblyPackagesDirectory = null;
        static Object s_syncroot = new object();

        public Catalog2ElfieOptions(Version indexerVersion, Version mergerVersion, string source, string downloadSource, double downloadPercentage, IStorageFactory storageFactory, int maxThreads, string tempPath, bool verbose)
        {
            this.IndexerVersion = indexerVersion;
            this.MergerVersion = mergerVersion;
            this.Source = source;
            this.DownloadSource = downloadSource;
            this.DownloadPercentage = downloadPercentage;
            this.StorageFactory = storageFactory;
            this.MaxThreads = maxThreads;
            this.TempPath = tempPath;
            this.Verbose = verbose;
        }

        public bool Verbose
        {
            get;
            private set;
        }

        public Version IndexerVersion
        {
            get;
            private set;
        }

        public Version MergerVersion
        {
            get;
            private set;
        }

        public string DownloadSource
        {
            get;
            private set;
        }

        public double DownloadPercentage
        {
            get;
            private set;
        }

        public string Source
        {
            get;
            private set;
        }

        public IStorageFactory StorageFactory
        {
            get;
            private set;
        }

        public int MaxThreads
        {
            get;
            private set;
        }

        public string TempPath
        {
            get;
            private set;
        }

        public static int RetryDelayInSeconds
        {
            get
            {
                if (!s_retryDelayInSeconds.HasValue)
                {
                    lock (s_syncroot)
                    {
                        if (!s_retryDelayInSeconds.HasValue)
                        {
                            s_retryDelayInSeconds = GetConfigurationValue("RetryDelay", 3);
                        }
                    }
                }

                return s_retryDelayInSeconds.Value;
            }
        }

        public static int FilterPackagesToIncludeBatchSize
        {
            get
            {
                if (!s_filterPackagesToIncludeBatchSize.HasValue)
                {
                    lock (s_syncroot)
                    {
                        if (!s_filterPackagesToIncludeBatchSize.HasValue)
                        {
                            s_filterPackagesToIncludeBatchSize = GetConfigurationValue("FilterPackagesToIncludeBatchSize", 500);
                        }
                    }
                }

                return s_filterPackagesToIncludeBatchSize.Value;
            }
        }

        public static int MinimumPackageCountFromDownloadUrl
        {
            get
            {
                if (!s_minimumPackageCountFromDownloadUrl.HasValue)
                {
                    lock (s_syncroot)
                    {
                        if (!s_minimumPackageCountFromDownloadUrl.HasValue)
                        {
                            s_minimumPackageCountFromDownloadUrl = GetConfigurationValue("MinimumPackageCountFromDownloadUrl", 50000);
                        }
                    }
                }

                return s_minimumPackageCountFromDownloadUrl.Value;
            }
        }

        public static int MinimumPackageCountAfterFiltering
        {
            get
            {
                if (!s_minimumPackageCountAfterFiltering.HasValue)
                {
                    lock (s_syncroot)
                    {
                        if (!s_minimumPackageCountAfterFiltering.HasValue)
                        {
                            s_minimumPackageCountAfterFiltering = GetConfigurationValue("MinimumPackageCountAfterFiltering", 4000);
                        }
                    }
                }

                return s_minimumPackageCountAfterFiltering.Value;
            }
        }

        public static int MinimumPackageCountInArdb
        {
            get
            {
                if (!s_minimumPackageCountInArdb.HasValue)
                {
                    lock (s_syncroot)
                    {
                        if (!s_minimumPackageCountInArdb.HasValue)
                        {
                            s_minimumPackageCountInArdb = GetConfigurationValue("MinimumPackageCountInArdb", 4000);
                        }
                    }
                }

                return s_minimumPackageCountInArdb.Value;
            }
        }

        public static int MinimumArdbTextSize
        {
            get
            {
                if (!s_minimumArdbTextSize.HasValue)
                {
                    lock (s_syncroot)
                    {
                        if (!s_minimumArdbTextSize.HasValue)
                        {
                            s_minimumArdbTextSize = GetConfigurationValue("MinimumArdbTextSize", 10000000);
                        }
                    }
                }

                return s_minimumArdbTextSize.Value;
            }
        }

        public static IEnumerable<string> RequiredPackages
        {
            get
            {
                if (s_requiredPackages == null)
                {
                    lock (s_syncroot)
                    {
                        if (s_requiredPackages == null)
                        {
                            string requiredPackagesText = ConfigurationManager.AppSettings["RequiredPackages"];
                            if (!String.IsNullOrWhiteSpace(requiredPackagesText))
                            {
                                List<String> packages = new List<String>();
                                string[] requiredPackagesParts = requiredPackagesText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string part in requiredPackagesParts)
                                {
                                    packages.Add(part.Trim().ToLowerInvariant());
                                }

                                s_requiredPackages = packages;
                            }
                        }
                    }
                }

                return s_requiredPackages;
            }
        }

        public static string AssemblyPackagesDirectory
        {
            get
            {
                if (s_assemblyPackagesDirectory == null)
                {
                    lock (s_syncroot)
                    {
                        if (s_assemblyPackagesDirectory == null)
                        {
                            string directory = ConfigurationManager.AppSettings["AssemblyPackagesDirectory"];

                            if (!String.IsNullOrWhiteSpace(directory))
                            {
                                s_assemblyPackagesDirectory = System.IO.Path.GetFullPath(directory);
                            }
                        }
                    }
                }

                return s_assemblyPackagesDirectory;
            }
        }

        private static int GetConfigurationValue(string appSettingName, int defaultValue)
        {
            string textFromConfig = ConfigurationManager.AppSettings[appSettingName];

            int value;
            if (!String.IsNullOrWhiteSpace(textFromConfig) && Int32.TryParse(textFromConfig, out value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }

        public static int GetRetryDelay(int retryAttempt)
        {
            int delay = (int)Math.Pow(Catalog2ElfieOptions.RetryDelayInSeconds, retryAttempt + 1);
            return delay;
        }

        private void Validate()
        {
            List<ArgumentException> exceptions = new List<ArgumentException>();

            if (String.IsNullOrWhiteSpace(this.Source))
            {
                exceptions.Add(new ArgumentException("Invalid -source parameter value."));
            }

            if (String.IsNullOrWhiteSpace(this.DownloadSource))
            {
                exceptions.Add(new ArgumentException("Invalid -downloadSource parameter value."));
            }

            if (this.DownloadPercentage < 0.1)
            {
                // Enforce a reasonable minimum percentage.
                exceptions.Add(new ArgumentException("Invalid -downloadPercentage parameter value. -downloadPercentage must be greater than 0.10."));
            }

            if (this.StorageFactory == null)
            {
                exceptions.Add(new ArgumentException("Invalid -storage* parameter values."));
            }

            if (this.MaxThreads <= 0)
            {
                exceptions.Add(new ArgumentException("Invalid -maxThreads parameter value. Value must be greater than zero."));
            }

            if (String.IsNullOrWhiteSpace(this.TempPath))
            {
                exceptions.Add(new ArgumentException("Invalid -tempPath parameter value. -tempPath must be non-empty."));
            }

            if (this.IndexerVersion == null)
            {
                exceptions.Add(new ArgumentException("Invalid -indexerVersion parameter value. -indexerVersion must be specified."));
            }
            else if (!ElfieCmd.DoesToolVersionExist(this.IndexerVersion))
            {
                exceptions.Add(new ArgumentException("Invalid -indexerVersion parameter value. -indexerVersion must be an available indexer version number."));
            }

            if (this.MergerVersion == null)
            {
                exceptions.Add(new ArgumentException("Invalid -mergerVersion parameter value. -mergerVersion must be specified."));
            }
            else if (!ElfieCmd.DoesToolVersionExist(this.MergerVersion))
            {
                exceptions.Add(new ArgumentException("Invalid -mergerVersion parameter value. -mergerVersion must be an available indexer version number."));
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException("Invalid arguments were passed to the application. See the inner exceptions for details.", exceptions.ToArray());
            }
        }

        public string ToText()
        {
            StringBuilder text = new StringBuilder();
            text.Append("IndexerVersion: " + this.IndexerVersion + Environment.NewLine);
            text.Append("MergerVersion: " + this.MergerVersion + Environment.NewLine);
            text.Append("Source: " + this.Source + Environment.NewLine);
            text.Append("DownloadSource: " + this.DownloadSource + Environment.NewLine);
            text.Append("DownloadPercentage: " + this.DownloadPercentage + Environment.NewLine);
            text.Append("StorageFactory: " + this.StorageFactory.ToString() + Environment.NewLine);
            text.Append("MaxThreads: " + this.MaxThreads + Environment.NewLine);
            text.Append("TempPath: " + this.TempPath + Environment.NewLine);
            text.Append("Verbose: " + this.Verbose + Environment.NewLine);

            return text.ToString();
        }

        public static Catalog2ElfieOptions FromArgs(string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);

            if (arguments == null)
            {
                throw new ArgumentOutOfRangeException("args", "Invalid command line arguments.");
            }

            bool verbose = CommandHelpers.GetVerbose(arguments);
            Version indexerVersion = CommandHelpers.GetIndexerVersion(arguments);
            Version mergerVersion = CommandHelpers.GetMergerVersion(arguments);
            string source = CommandHelpers.GetSource(arguments);
            string downloadSource = CommandHelpers.GetDownloadSource(arguments);
            double downloadPercentage = CommandHelpers.GetDownloadPercentage(arguments);
            IStorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            int interval = CommandHelpers.GetInterval(arguments);
            int maxThreads = CommandHelpers.GetMaxThreads(arguments);
            string tempPath = CommandHelpers.GetTempPath(arguments);

            Catalog2ElfieOptions options = new Catalog2ElfieOptions(indexerVersion, mergerVersion, source, downloadSource, downloadPercentage, storageFactory, maxThreads, tempPath, verbose);
            options.Validate();

            return options;
        }
    }
}

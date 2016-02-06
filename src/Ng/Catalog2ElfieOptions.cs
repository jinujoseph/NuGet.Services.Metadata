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
using System.Text;
using Ng.Elfie;

namespace Ng
{
    class Catalog2ElfieOptions
    {
        public Catalog2ElfieOptions(string indexerVersion, string source, IStorageFactory storageFactory, int interval, int maxThreads, string tempPath, bool verbose)
        {
            this.IndexerVersion = indexerVersion;
            this.Source = source;
            this.StorageFactory = storageFactory;
            this.Interval = interval;
            this.MaxThreads = maxThreads;
            this.TempPath = tempPath;
            this.Verbose = verbose;
        }

        public bool Verbose
        {
            get;
            private set;
        }

        public string IndexerVersion
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

        public int Interval
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

        private void Validate()
        {
            List<ArgumentException> exceptions = new List<ArgumentException>();

            if (String.IsNullOrWhiteSpace(this.Source))
            {
                exceptions.Add(new ArgumentException("Invalid -source parameter value."));
            }

            if (this.StorageFactory == null)
            {
                exceptions.Add(new ArgumentException("Invalid -storage* parameter values."));
            }

            if (this.Interval < 0)
            {
                exceptions.Add(new ArgumentException("Invalid -interval parameter value. Value must be greater than or equal to zero."));
            }

            if (this.MaxThreads <= 0)
            {
                exceptions.Add(new ArgumentException("Invalid -maxthreads parameter value. Value must be greater than zero."));
            }

            if (String.IsNullOrWhiteSpace(this.TempPath))
            {
                exceptions.Add(new ArgumentException("Invalid -tempPath parameter value. -tempPath must be non-empty."));
            }

            if (String.IsNullOrWhiteSpace(this.IndexerVersion))
            {
                exceptions.Add(new ArgumentException("Invalid -indexerVersion parameter value. -indexerVersion must be specified."));
            }

            if (!ElfieCmd.DoesVersionExist(this.IndexerVersion))
            {
                exceptions.Add(new ArgumentException("Invalid -indexerVersion parameter value. -indexerVersion must be an available indexer version number."));
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
            text.Append("Source: " + this.Source + Environment.NewLine);
            text.Append("StorageFactory: " + this.StorageFactory.ToString() + Environment.NewLine);
            text.Append("Interval: " + this.Interval + Environment.NewLine);
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
            string indexerVersion = CommandHelpers.GetIndexerVersion(arguments);
            string source= CommandHelpers.GetSource(arguments);
            IStorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            int interval = CommandHelpers.GetInterval(arguments);
            int maxThreads = CommandHelpers.GetMaxThreads(arguments);
            string tempPath = CommandHelpers.GetTempPath(arguments);

            Catalog2ElfieOptions options = new Catalog2ElfieOptions(indexerVersion, source, storageFactory, interval, maxThreads, tempPath, verbose);
            options.Validate();

            return options;
        }
    }
}

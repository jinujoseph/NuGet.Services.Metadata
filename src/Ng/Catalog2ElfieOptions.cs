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

namespace Ng
{
    class Catalog2ElfieOptions
    {
        public Catalog2ElfieOptions(string source, IStorageFactory storageFactory, int interval, int maxThreads, bool verbose)
        {
            this.Source = source;
            this.StorageFactory = storageFactory;
            this.Interval = interval;
            this.MaxThreads = maxThreads;
            this.Verbose = verbose;
        }

        public bool Verbose
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

            if (exceptions.Count > 0)
            {
                throw new AggregateException("Invalid arguments were passed to the application. See the inner exceptions for details.", exceptions.ToArray());
            }
        }

        public string ToText()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Source: " + this.Source + Environment.NewLine);
            text.Append("StorageFactory: " + this.StorageFactory.ToString() + Environment.NewLine);
            text.Append("Interval: " + this.Interval + Environment.NewLine);
            text.Append("MaxThreads: " + this.MaxThreads + Environment.NewLine);
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
            string source= CommandHelpers.GetSource(arguments);
            IStorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            int interval = CommandHelpers.GetInterval(arguments);
            int maxThreads = CommandHelpers.GetMaxThreads(arguments);

            Catalog2ElfieOptions options = new Catalog2ElfieOptions(source, storageFactory, interval, maxThreads, verbose);
            options.Validate();

            return options;
        }
    }
}

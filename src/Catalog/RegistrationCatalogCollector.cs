﻿using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog
{
    public class RegistrationCatalogCollector : SortingGraphCollector
    {
        StorageFactory _storageFactory;

        public RegistrationCatalogCollector(Uri index, StorageFactory storageFactory, Func<HttpMessageHandler> handlerFunc = null, int batchSize = 200)
            : base(index, new Uri[] { Schema.DataTypes.Package }, handlerFunc, batchSize)
        {
            _storageFactory = storageFactory;

            ContentBaseAddress = new Uri("http://tempuri.org");

            PartitionSize = 64;
            PackageCountThreshold = 128;
        }

        public Uri ContentBaseAddress { get; set; }
        public int PartitionSize { get; set; }
        public int PackageCountThreshold { get; set; }

        protected override Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            return RegistrationCatalogCreator.ProcessGraphs(
                sortedGraphs.Key,
                sortedGraphs.Value,
                _storageFactory,
                ContentBaseAddress,
                PartitionSize,
                PackageCountThreshold);
        }
    }
}
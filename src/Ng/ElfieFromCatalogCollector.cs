// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;

namespace Ng
{
    /// <summary>
    /// Creates Elfie index (Idx) files for NuGet packages.
    /// </summary>
    public class ElfieFromCatalogCollector : CommitCollector
    {
        Storage _storage;
        int _maxThreads;

        public ElfieFromCatalogCollector(Uri index, Storage storage, int maxThreads, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            this._storage = storage;
            this._maxThreads = maxThreads;
        }

        /// <summary>
        /// Processes the next set of NuGet packages from the catalog.
        /// </summary>
        /// <returns>True if the batch processing should continue. Otherwise false.</returns>
        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity OnProcessBatch");

            // Get the catalog entries for the packages in this batch
            IEnumerable<JObject> catalogItems = await FetchCatalogItems(client, items, cancellationToken);

            // Process each of the packages.
            ProcessCatalogItems(catalogItems);

            Trace.TraceInformation("#StopActivity OnProcessBatch");

            return true;
        }

        /// <summary>
        /// Downloads the catalog entries for a set of NuGet packages.
        /// </summary>
        /// <param name="client">The HttpClient which will download the catalog entires.</param>
        /// <param name="items">The list of packages to download catalog entires for.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The catalog entires for the packages specified by the 'items' parameter.</returns>
        /// <remarks>The catalog entries are a json files which describe basic information about a package.
        /// For example: https://api.nuget.org/v3/catalog0/data/2015.02.02.16.48.21/angularjs.1.0.2.json
        /// </remarks>
        async Task<IEnumerable<JObject>> FetchCatalogItems(CollectorHttpClient client, IEnumerable<JToken> items, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity FetchCatalogItems");

            IList<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JToken item in items)
            {
                Uri catalogItemUri = item["@id"].ToObject<Uri>();

                tasks.Add(client.GetJObjectAsync(catalogItemUri, cancellationToken));
            }

            await Task.WhenAll(tasks);

            Trace.TraceInformation("#StopActivity FetchCatalogItems");

            return tasks.Select(t => t.Result);
        }

        /// <summary>
        /// Enumerates through the catalog enties and processes each entry.
        /// </summary>
        /// <param name="catalogItems">The list of catalog entires to process.</param>
        void ProcessCatalogItems(IEnumerable<JObject> catalogItems)
        {
            Trace.TraceInformation("#StartActivity ProcessCatalogItems");

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = this._maxThreads,
            };

            Parallel.ForEach(catalogItems, options, catalogItem =>
            {
                Trace.TraceInformation("Processing CatalogItem {0}", catalogItem["@id"]);

                NormalizeId(catalogItem);

                if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDetails))
                {
                    ProcessPackageDetails(catalogItem);
                }
                else if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDelete))
                {
                    ProcessPackageDelete(catalogItem);
                }
                else
                {
                    Trace.TraceWarning("Unrecognized @type ignoring CatalogItem");
                }
            });

            Trace.TraceInformation("#StopActivity ProcessCatalogItems");
        }

        /// <summary>
        /// Process an individual catalog item (NuGet pacakge) which has been added or updated in the catalog
        /// </summary>
        /// <param name="catalogItem">The catalog item to process.</param>
        void ProcessPackageDetails(JObject catalogItem)
        {
            if (IsListed(catalogItem))
            {
                Trace.TraceInformation("Processing listed package " + catalogItem["@id"].Value<string>());
            }
        }

        /// <summary>
        /// Process an individual catalog item (NuGet pacakge) which has been deleted from the catalog
        /// </summary>
        /// <param name="catalogItem">The catalog item to process.</param>
        void ProcessPackageDelete(JObject catalogItem)
        {
            Trace.TraceInformation("Processing deleted package " + catalogItem["@id"].Value<string>());
        }

        /// <summary>
        /// Replaces the catalog entry id with the originalId, if it exists.
        /// </summary>
        void NormalizeId(JObject catalogItem)
        {
            JToken originalId = catalogItem["originalId"];
            if (originalId != null)
            {
                catalogItem["id"] = originalId.ToString();
            }
        }

        /// <summary>
        /// Gets the context node of a catalog entry
        /// </summary>
        JToken GetContext(JObject catalogItem)
        {
            return catalogItem["@context"];
        }

        /// <summary>
        /// Determines if a catalog entry is listed based on its published data.
        /// </summary>
        /// <param name="catalogItem"></param>
        /// <returns>True if the catalog entry is listed. Otherwise false.</returns>
        bool IsListed(JObject catalogItem)
        {
            JToken publishedValue;
            if (catalogItem.TryGetValue("published", out publishedValue))
            {
                var publishedDate = int.Parse(publishedValue.ToObject<DateTime>().ToString("yyyyMMdd"));
                return (publishedDate != 19000101);
            }
            else
            {
                return true;
            }
        }
    }
}

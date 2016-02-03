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

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("#StartActivity OnProcessBatch");

            IEnumerable<JObject> catalogItems = await FetchCatalogItems(client, items, cancellationToken);

            ProcessCatalogItems(catalogItems);

            Trace.TraceInformation("#StopActivity OnProcessBatch");

            return true;
        }

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

        void ProcessPackageDetails(JObject catalogItem)
        {
            if (IsListed(catalogItem))
            {
                Trace.TraceInformation("Processing listed package " + catalogItem["@id"].Value<string>());
            }
        }

        void ProcessPackageDelete(JObject catalogItem)
        {
            Trace.TraceInformation("Processing deleted package " + catalogItem["@id"].Value<string>());
        }

        void NormalizeId(JObject catalogItem)
        {
            JToken originalId = catalogItem["originalId"];
            if (originalId != null)
            {
                catalogItem["id"] = originalId.ToString();
            }
        }

        JToken GetContext(JObject catalogItem)
        {
            return catalogItem["@context"];
        }

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

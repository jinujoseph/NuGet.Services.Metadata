﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ng.Persistence;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Models
{
    public class PackageCatalog : JsonStorageItem
    {
        public PackageCatalog(Uri catalog, Uri address, IStorage storage) : base(address, storage)
        {
            this.Catalog = catalog;
            this.Packages = new SortedList<string, PackageInfo>();
        }

        [JsonProperty("catalog")]
        public Uri Catalog
        {
            get;
            set;
        }

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated
        {
            get;
            set;
        }

        [JsonProperty("packages")]
        public SortedList<string, PackageInfo> Packages
        {
            get;
            set;
        }

        public override async Task LoadAsync(Uri address, IStorage storage, CancellationToken cancellationToken)
        {
            string json = await storage.LoadString(address, cancellationToken);

            if (json == null)
            {
                throw new ArgumentOutOfRangeException("The address did not contain a storage file.");
            }

            PackageCatalog item = JsonConvert.DeserializeObject<PackageCatalog>(json);

            this.Catalog = item.Catalog;
            this.LastUpdated = item.LastUpdated;

            if (item.Packages != null)
            {
                this.Packages = item.Packages;
            }
        }
    }
}


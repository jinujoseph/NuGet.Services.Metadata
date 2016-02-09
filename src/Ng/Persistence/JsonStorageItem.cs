// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ng.Persistence;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Persistence
{
    public abstract class JsonStorageItem
    {
        protected JsonStorageItem(Uri address, IStorage storage)
        {
            this.Address = address;
            this.Storage = storage;
        }

        [JsonIgnore]
        public Uri Address
        {
            get;
            protected set;
        }

        [JsonIgnore]
        public IStorage Storage
        {
            get;
            protected set;
        }

        public async virtual Task SaveAsync(CancellationToken cancellationToken)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (StorageContent content = new StringStorageContent(json, "application/json", "no-store"))
            {
                await this.Storage.Save(this.Address, content, cancellationToken);
            }
        }

        public abstract Task LoadAsync(Uri address, IStorage storage, CancellationToken cancellationToken);
    }
}


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
    /// <summary>
    /// A storage item which is saved to storage as json.
    /// </summary>
    public abstract class JsonStorageItem
    {
        /// <summary>
        /// Creates a new JsonStorageItem.
        /// </summary>
        /// <param name="storage">The storage object responsible for loading and saving the file.</param>
        /// <param name="address">The resource URI which specifies where to save the file.</param>
        protected JsonStorageItem(IStorage storage, Uri address)
        {
            this.Address = address;
            this.Storage = storage;
        }

        /// <summary>
        /// The storage object responsible for loading and saving the file.
        /// </summary>
        [JsonIgnore]
        public IStorage Storage
        {
            get;
            protected set;
        }

        /// <summary>
        /// The resource URI which specifies where to save the file.
        /// </summary>
        [JsonIgnore]
        public Uri Address
        {
            get;
            protected set;
        }

        /// <summary>
        /// Saves the file to storage.
        /// </summary>
        public async virtual Task SaveAsync(CancellationToken cancellationToken)
        {
            // BUGBUG: When we're satisifed with this format, we should turn off indenting. We'll get perf improvement saving the file.
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (StorageContent content = new StringStorageContent(json, "application/json", "no-store"))
            {
                await this.Storage.Save(this.Address, content, cancellationToken);
            }
        }

        /// <summary>
        ///  Loads the file from storage.
        /// </summary>
        public abstract Task LoadAsync(Uri address, IStorage storage, CancellationToken cancellationToken);
    }
}


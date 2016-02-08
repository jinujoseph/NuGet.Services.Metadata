using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng.Persistence
{
    abstract class JsonStorageItem
    {
        protected JsonStorage(Uri address, IStorage storage)
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

        public abstract Task SaveAsync(CancellationToken cancellationToken);

        public abstract Task LoadAsync(Uri address, IStorage storage, CancellationToken cancellationToken);
    }
}


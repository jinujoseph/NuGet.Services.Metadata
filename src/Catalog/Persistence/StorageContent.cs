// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class StorageContent : IDisposable
    {
        public string ContentType
        {
            get;
            set;
        }

        public string CacheControl
        {
            get;
            set;
        }

        public abstract Stream GetContentStream();

        #region IDisposable Support  
        private bool _disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}

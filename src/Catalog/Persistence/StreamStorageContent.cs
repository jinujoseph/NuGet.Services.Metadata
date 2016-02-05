// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class StreamStorageContent : StorageContent
    {
        public StreamStorageContent(Stream content, string contentType = "", string cacheControl = "")
        {
            Content = content;
            ContentType = contentType;
            CacheControl = cacheControl;
        }

        public Stream Content
        {
            get;
            set;
        }

        public override Stream GetContentStream()
        {
            return Content;
        }

        #region IDisposable Support  
        private bool _disposedValue = false;

        protected override void Dispose(bool disposing)
        {
            if (!this._disposedValue)
            {
                if (disposing)
                {
                    // Dispose the content stream.  
                    if (this.Content != null)
                    {
                        this.Content.Dispose();
                    }
                }

                this._disposedValue = true;

                base.Dispose(disposing);
            }
        }

        #endregion
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ng;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using Xunit;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Persistence
{
    public class StreamStorageContentTests
    {
        [Fact]
        public void Dispose_MemoryStream()
        {
            using (MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
            {
                // Arrange
                StreamStorageContent content = new StreamStorageContent(stream);

                // Act
                content.Dispose();

                // Assert
                Assert.Throws<ObjectDisposedException>(delegate { long l = stream.Length; });
            }
        }

        [Fact]
        public void Dispose_MemoryStreamAlreadyDisposed()
        {
            using (MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
            {
                // Arrange
                StreamStorageContent content = new StreamStorageContent(stream);
                stream.Dispose();

                // Act
                content.Dispose();

                // Assert
                Assert.Throws<ObjectDisposedException>(delegate { long l = stream.Length; });
            }
        }


        [Fact]
        public void Dispose_NullStream()
        {
            // Arrange
            StreamStorageContent content = new StreamStorageContent(null);

            // Act
            content.Dispose();

            // Assert
        }
    }
}

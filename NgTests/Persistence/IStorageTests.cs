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
    public class IStorageTests
    {
        [Theory]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3.4", "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg")]
        [InlineData("http://microsoft.com/storage/v1", "My.Shiny.New.Package", "1.2.3.4", "glitter.nupkg", "http://microsoft.com/storage/v1/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg")]
        [InlineData("http://microsoft.com/storage/Version 1.1/Alpha/", "My.Shiny.New.Package", "1.2.3.4", "glitter.nupkg", "http://microsoft.com/storage/Version 1.1/Alpha/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg")]
        [InlineData("http://microsoft.com/storage/", "清原容.疑者", "1.2.3.4", "相事務.nupkg", "http://microsoft.com/storage/packages/清原容.疑者/1.2.3.4/相事務.nupkg")]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3-beta1", "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3-beta1/glitter.nupkg")]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3.4.5.6.7.8", "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4.5.6.7.8/glitter.nupkg")]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3.4", "glitter", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter")]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3.4", "glitter.zip", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.zip")]
        [InlineData("http://microsoft.com/storage/", "a<b>c:d", "1.2.3.4", "glitter.nupkg", "http://microsoft.com/storage/packages/a-b-c-d/1.2.3.4/glitter.nupkg")]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "a<b>c:d", "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/a-b-c-d/glitter.nupkg")]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3.4", "a<b>c:d.a<b>c:d", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/a-b-c-d.a-b-c-d")]
        public void ComposePackageResourceUrl_ValidInputs(string baseAddress, string packageId, string packageVersion, string filename, string expectedAddress)
        {
            // Arrange
            Uri baseUri = new Uri(baseAddress);
            MemoryStorage storage = new MemoryStorage(baseUri);

            // Act
            Uri registrationUri = storage.ComposePackageResourceUrl(packageId, packageVersion, filename);

            // Assert
            Uri expectedUri = new Uri(expectedAddress);
            Assert.True(registrationUri.Equals(expectedUri));
        }

        [Theory]
        [InlineData("http://microsoft.com/storage/", null, "1.2.3.4", "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg", typeof(ArgumentNullException))]
        [InlineData("http://microsoft.com/storage/", "", "1.2.3.4", "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg", typeof(ArgumentNullException))]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", null, "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg", typeof(ArgumentNullException))]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "", "glitter.nupkg", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg", typeof(ArgumentNullException))]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3.4", null, "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg", typeof(ArgumentNullException))]
        [InlineData("http://microsoft.com/storage/", "My.Shiny.New.Package", "1.2.3.4", "", "http://microsoft.com/storage/packages/my.shiny.new.package/1.2.3.4/glitter.nupkg", typeof(ArgumentNullException))]
        public void ComposePackageResourceUrl_InvalidInputs(string baseAddress, string packageId, string packageVersion, string filename, string expectedAddress, Type expectedException)
        {
            // Arrange
            Uri baseUri = new Uri(baseAddress);
            MemoryStorage storage = new MemoryStorage(baseUri);

            // Act
            Action action = delegate
            {
                Uri registrationUri = storage.ComposePackageResourceUrl(packageId, packageVersion, filename);
            };

            // Assert
            Assert.Throws(expectedException, action);
        }
    }
}

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
using Catalog = NuGet.Services.Metadata.Catalog;
using Xunit;
using Ng.Models;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Models
{
    public class PackageCatalogTests
    {
        [Fact]
        public void Load()
        {
            // Arrange
            Uri catalog = new Uri("http://foo");
            Uri baseAddress = new Uri("http://storage");
            MemoryStorage storage = new MemoryStorage(baseAddress);
            Uri fileAddress = new Uri("http://storage/packagecatalog.json");
            storage.Content.Add(fileAddress, new StringStorageContent(inputJson));

            // Act
            PackageCatalog packageCatalog = new PackageCatalog(catalog, storage, fileAddress, null);
            packageCatalog.LoadAsync(fileAddress, storage, new CancellationToken()).Wait();

            // Assert
            string key = "51Degrees.mobi".ToLowerInvariant();
            PackageInfo package = packageCatalog.Packages[key];

            Assert.Equal(3, packageCatalog.Packages.Count);
            Assert.Equal("51Degrees.mobi", package.PackageId);
            Assert.Equal("3.2.5.6", package.LatestStableVersion);
            Assert.Equal(false, package.HaveIdx);
            Assert.Equal(new Guid("788b39a0-9a58-4000-88be-599a0ad2a8a9"), package.CommitId);
            Assert.Equal(DateTime.Parse("2015-12-07T14:36:57.142124Z").ToUniversalTime(), package.CommitTimeStamp);
            Assert.Equal(new Uri("https://api.nuget.org/packages/51degrees.mobi.3.2.5.6.nupkg"), package.DownloadUrl);
        }

        [Fact]
        public void Delist()
        {
            // Arrange
            Uri catalog = new Uri("http://foo");
            Uri baseAddress = new Uri("http://storage");
            MemoryStorage storage = new MemoryStorage(baseAddress);
            Uri fileAddress = new Uri("http://storage/packagecatalog.json");
            storage.Content.Add(fileAddress, new StringStorageContent(inputJson));

            // Act
            PackageCatalog packageCatalog = new PackageCatalog(catalog, storage, fileAddress, null);
            packageCatalog.LoadAsync(fileAddress, storage, new CancellationToken()).Wait();
            string key = "51Degrees.mobi".ToLowerInvariant();
            packageCatalog.DelistPackage(key);

            // Assert
            Assert.Equal(2, packageCatalog.Packages.Count);

            Action action = delegate
            {
                Assert.Null(packageCatalog.Packages[key]);
            };

            Assert.Throws(typeof(KeyNotFoundException), action);
        }

        [Fact]
        public void SetLatestStablePackage_Add()
        {
            // Arrange
            Uri catalog = new Uri("http://foo");
            Uri baseAddress = new Uri("http://storage");
            MemoryStorage storage = new MemoryStorage(baseAddress);
            Uri fileAddress = new Uri("http://storage/packagecatalog.json");
            storage.Content.Add(fileAddress, new StringStorageContent(inputJson));

            // Act
            PackageCatalog packageCatalog = new PackageCatalog(catalog, storage, fileAddress, null);
            packageCatalog.LoadAsync(fileAddress, storage, new CancellationToken()).Wait();
            packageCatalog.SetLatestStablePackage("My.Shiny.New.Package", "99.99.99-prerelease", new Guid(), DateTime.MinValue, new Uri("http://foo/download"), true);

            // Assert
            string key = "My.Shiny.New.Package".ToLowerInvariant();
            PackageInfo package = packageCatalog.Packages[key];

            Assert.Equal(4, packageCatalog.Packages.Count);
            Assert.Equal("My.Shiny.New.Package", package.PackageId);
            Assert.Equal("99.99.99-prerelease", package.LatestStableVersion);
            Assert.Equal(true, package.HaveIdx);
            Assert.Equal(new Guid(), package.CommitId);
            Assert.Equal(DateTime.MinValue, package.CommitTimeStamp);
            Assert.Equal(new Uri("http://foo/download"), package.DownloadUrl);
        }

        [Fact]
        public void SetLatestStablePackage_Update()
        {
            // Arrange
            Uri catalog = new Uri("http://foo");
            Uri baseAddress = new Uri("http://storage");
            MemoryStorage storage = new MemoryStorage(baseAddress);
            Uri fileAddress = new Uri("http://storage/packagecatalog.json");
            storage.Content.Add(fileAddress, new StringStorageContent(inputJson));

            // Act
            PackageCatalog packageCatalog = new PackageCatalog(catalog, storage, fileAddress, null);
            packageCatalog.LoadAsync(fileAddress, storage, new CancellationToken()).Wait();
            packageCatalog.SetLatestStablePackage("51Degrees.mobi", "9.9.9.9", new Guid(), DateTime.MinValue, new Uri("http://foo/download"), true);

            // Assert
            string key = "51Degrees.mobi".ToLowerInvariant();
            PackageInfo package = packageCatalog.Packages[key];

            Assert.Equal(3, packageCatalog.Packages.Count);
            Assert.Equal("51Degrees.mobi", package.PackageId);
            Assert.Equal("9.9.9.9", package.LatestStableVersion);
            Assert.Equal(true, package.HaveIdx);
            Assert.Equal(new Guid(), package.CommitId);
            Assert.Equal(DateTime.MinValue, package.CommitTimeStamp);
            Assert.Equal(new Uri("http://foo/download"), package.DownloadUrl);
        }

        [Theory]
        [InlineData("51Degrees.mobi", "3.2.5.6", true, 3, true, false)]
        [InlineData("51Degrees.mobi", "3.2.5", true, 3, false, false)]
        [InlineData("51Degrees", "3.2.5", true, 3, false, true)]
        public void UpdateLatestStablePackage(string packageId, string packageVersion, bool haveIdx, int expectedPackageCount, bool expectedHaveIdx, bool packageIsNull)
        {
            // Arrange
            Uri catalog = new Uri("http://foo");
            Uri baseAddress = new Uri("http://storage");
            MemoryStorage storage = new MemoryStorage(baseAddress);
            Uri fileAddress = new Uri("http://storage/packagecatalog.json");
            storage.Content.Add(fileAddress, new StringStorageContent(inputJson));

            // Act
            PackageCatalog packageCatalog = new PackageCatalog(catalog, storage, fileAddress, null);
            packageCatalog.LoadAsync(fileAddress, storage, new CancellationToken()).Wait();
            packageCatalog.UpdateLatestStablePackage(packageId, packageVersion, haveIdx);

            // Assert
            PackageInfo package;
            string key = packageId.ToLowerInvariant();
            packageCatalog.Packages.TryGetValue(key, out package);

            Assert.Equal(expectedPackageCount, packageCatalog.Packages.Count);
            Assert.Equal(packageIsNull, package == null);

            if (!packageIsNull)
            {
                Assert.Equal(expectedHaveIdx, package.HaveIdx);
            }
        }

        string inputJson = @"{
                              ""catalog"": ""http://api.nuget.org/v3/catalog0/index.json"",
                              ""lastUpdated"": ""0001-01-01T00:00:00"",
                              ""packages"": {
                                ""51degrees.mobi"": {
                                  ""id"": ""51Degrees.mobi"",
                                  ""latestStableVersion"": ""3.2.5.6"",
                                  ""haveIdx"": false,
                                  ""commitId"": ""788b39a0-9a58-4000-88be-599a0ad2a8a9"",
                                  ""commitTimeStamp"": ""2015-12-07T14:36:57.142124Z"",
                                  ""downloadUrl"": ""https://api.nuget.org/packages/51degrees.mobi.3.2.5.6.nupkg""
                                },
                                ""51degrees.mobi-webmatrix"": {
                                  ""id"": ""51Degrees.mobi-WebMatrix"",
                                  ""latestStableVersion"": ""3.2.5.6"",
                                  ""haveIdx"": false,
                                  ""commitId"": ""39ff0c67-b711-4e9d-96ae-3559ec92ee4b"",
                                  ""commitTimeStamp"": ""2015-12-07T14:41:44.6299698Z"",
                                  ""downloadUrl"": ""https://api.nuget.org/packages/51degrees.mobi-webmatrix.3.2.5.6.nupkg""
                                },
                                ""ae.net.mail"": {
                                  ""id"": ""AE.Net.Mail"",
                                  ""latestStableVersion"": ""1.7.10"",
                                  ""haveIdx"": false,
                                  ""commitId"": ""58ff9400-4151-48b4-875a-cff52a521566"",
                                  ""commitTimeStamp"": ""2015-04-21T19:03:02.3947589Z"",
                                  ""downloadUrl"": ""https://api.nuget.org/packages/ae.net.mail.1.7.10.nupkg""
                                },
                              }
                            }";
    }
}
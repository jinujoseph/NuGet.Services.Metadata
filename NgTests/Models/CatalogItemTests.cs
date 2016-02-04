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

namespace NgTests.Models
{
    public class CatalogItemTests
    {
        [Fact]
        public void Deserialize_ValidJsonFile()
        {
            // Arrange
            string inputJson = @"{
                                     ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.02.16.48.21/angularjs.1.0.2.json"",
                                     ""@type"": [
                                        ""PackageDetails""
                                     ],
                                     ""id"": ""angularjs"",
                                     ""version"": ""1.0.2"",
                                 }";

            // Act
            CatalogItem item = CatalogItem.Deserialize(inputJson);

            // Assert
            Assert.True(item.PackageId == "angularjs");
            Assert.True(item.PackageVersion == "1.0.2");
        }

        [Theory]
        [InlineData(null, typeof(ArgumentNullException))]
        [InlineData("", typeof(ArgumentOutOfRangeException))]
        [InlineData("{}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""@id"": ""https://foo"", ""@type"": [""PackageDetails""], ""packageId"": ""angularjs"", ""packageVersion"": ""1.0.2"",}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""@id"": ""https://foo"", ""@type"": [""PackageDetails""], ""version"": ""1.0.2"",}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""@id"": ""https://foo"", ""@type"": [""PackageDetails""], ""id"": ""angularjs"",}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""@id"": ""https://foo"", ""@type"": [""PackageDetails""], ""packageId"": """", ""packageVersion"": """",}", typeof(ArgumentOutOfRangeException))]
        public void Deserialize_InvalidJsonFile(string inputJson, Type expectedException)
        {
            // Arrange

            // Act
            Action action = delegate
            {
                CatalogItem item = CatalogItem.Deserialize(inputJson);
            };

            // Assert
            Assert.Throws(expectedException, action);
        }
    }
}
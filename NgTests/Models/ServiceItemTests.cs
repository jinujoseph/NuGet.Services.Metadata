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
    public class ServiceItemTests
    {
        const string _validServiceIndexJson = @"{
                                              ""version"": ""3.0.0-beta.1"",
                                              ""resources"": [
                                                {
                                                  ""@id"": ""https://api-v3search-0.nuget.org/query"",
                                                  ""@type"": ""SearchQueryService"",
                                                  ""comment"": ""Query endpoint of NuGet Search service (primary)""
                                                },
                                                {
                                                  ""@id"": ""https://api.nuget.org/v3/registration1/"",
                                                  ""@type"": ""RegistrationsBaseUrl"",
                                                  ""comment"": ""Base URL of Azure storage where NuGet package registration info is stored""
                                                },
                                             ],
                                          }";

        [Fact]
        public void Deserialize_ValidJsonFile()
        {
            // Arrange
            string inputJson = ServiceItemTests._validServiceIndexJson;

            // Act
            ServiceIndex item = ServiceIndex.Deserialize(inputJson);

            // Assert
            ServiceIndexResource registrationResource = item.Resources.Where(r => r.Type.Equals("RegistrationsBaseUrl")).FirstOrDefault();
            Assert.NotNull(registrationResource);
            Assert.True(registrationResource.Id.Equals(new Uri("https://api.nuget.org/v3/registration1/")));
        }

        [Theory]
        [InlineData(null, typeof(ArgumentNullException))]
        [InlineData("", typeof(ArgumentOutOfRangeException))]
        [InlineData("{}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""version"": ""0"", ""resources"": [{""@id"": ""id1"", ""@type"": ""SearchQueryService"", ""comment"": ""comment1""},],}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""version"": ""0"", ""resources"": [{""@id"": ""id1"", ""@type"": ""SearchQueryService"", ""comment"": ""comment1""}, {""@type"": ""RegistrationsBaseUrl"", ""comment"": ""comment2""},],}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""version"": ""0"", ""resources"": [{""@id"": ""id1"", ""@type"": ""SearchQueryService"", ""comment"": ""comment1""}, {""@id"": ""id2"", ""comment"": ""comment2""},],}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""version"": ""0"", ""resources"": [{""@id"": ""id1"", ""@type"": ""SearchQueryService"", ""comment"": ""comment1""}, {""@id"": """", ""@type"": ""RegistrationsBaseUrl"", ""comment"": ""comment2""},],}", typeof(ArgumentOutOfRangeException))]
        public void Deserialize_InvalidJsonFile(string inputJson, Type expectedException)
        {
            // Arrange

            // Act
            Action action = delegate
            {
                ServiceIndex item = ServiceIndex.Deserialize(inputJson);
            };

            // Assert
            Assert.Throws(expectedException, action);
        }

        [Theory]
        [InlineData(_validServiceIndexJson, "SearchQueryService", "https://api-v3search-0.nuget.org/query")]
        [InlineData(_validServiceIndexJson, "RegistrationsBaseUrl", "https://api.nuget.org/v3/registration1/")]
        public void TryGetResourceId_ValidInput(string inputJson, string resourceType, string resourceId)
        {
            // Arrange
            ServiceIndex item = ServiceIndex.Deserialize(inputJson);

            // Act
            Uri resourceUrl;
            bool success = item.TryGetResourceId(resourceType, out resourceUrl);

            // Assert
            Assert.True(success);
            Assert.True(resourceUrl.OriginalString.Equals(resourceId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("SEARCHQUERYSERVICE")]
        [InlineData("Search")]
        public void TryGetResourceId_InvalidInputType(string inputType)
        {
            // Arrange
            ServiceIndex item = ServiceIndex.Deserialize(ServiceItemTests._validServiceIndexJson);

            // Act
            Uri resourceUrl;
            bool success = item.TryGetResourceId(inputType, out resourceUrl);

            // Assert
            Assert.False(success);
            Assert.True(resourceUrl == null);
        }
    }
}
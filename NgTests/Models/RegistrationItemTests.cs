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
    public class RegistrationItemTests
    {
        const string _validRegistrationItemJson = @"{
                                                      ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/8.0.2.json"",
                                                      ""@type"": [
                                                        ""Package"",
                                                      ],
                                                      ""catalogEntry"": ""https://api.nuget.org/v3/catalog0/data/2016.01.09.01.07.08/newtonsoft.json.8.0.2.json"",
                                                      ""listed"": true,
                                                      ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.8.0.2.nupkg"",
                                                      ""published"": ""2016-01-09T01:06:39.39Z"",
                                                      ""registration"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json"",
                                                   }";

        [Fact]
        public void Deserialize_ValidJsonFile()
        {
            // Arrange
            string inputJson = RegistrationItemTests._validRegistrationItemJson;

            // Act
            RegistrationItem item = RegistrationItem.Deserialize(inputJson);

            // Assert
            Assert.True(item.Registration.Equals(new Uri("https://api.nuget.org/v3/registration1/newtonsoft.json/index.json")));
            Assert.True(item.PackageContent.Equals(new Uri("https://api.nuget.org/packages/newtonsoft.json.8.0.2.nupkg")));
            Assert.True(item.Listed);
        }

        [Theory]
        [InlineData(null, typeof(ArgumentNullException))]
        [InlineData("", typeof(ArgumentOutOfRangeException))]
        [InlineData("{}", typeof(ArgumentOutOfRangeException))]
        [InlineData(@"{""@id"": ""id1"", ""@type"": [""type1"",], ""catalogEntry"": ""catalogEntry1"", ""listed"": true, ""registration"": ""registration1"",}", typeof(ArgumentOutOfRangeException))]
        public void Deserialize_InvalidJsonFile(string inputJson, Type expectedException)
        {
            // Arrange

            // Act
            Action action = delegate
            {
                RegistrationItem item = RegistrationItem.Deserialize(inputJson);
            };

            // Assert
            Assert.Throws(expectedException, action);
        }
    }
}
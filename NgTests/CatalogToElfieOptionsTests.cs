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

namespace NgTests
{
    public class CatalogToElfieOptionsTests
    {
        [Theory]
        [InlineData("catalog2elfie -source http://api.nuget.org/v3/catalog0/index.json -storageBaseAddress file:///C:/NuGet -storageType file -storagePath C:\\NuGet -maxthreads 1 -verbose true")]
        public void Validate_ValidValues(string inputArgs)
        {
            // Arrange
            string[] args = inputArgs.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Act
            Catalog2ElfieOptions options = Catalog2ElfieOptions.FromArgs(args);

            // Assert
        }

        [Theory]
        [InlineData(null, typeof(ArgumentNullException))]
        [InlineData("", typeof(ArgumentOutOfRangeException))]
        [InlineData("catalog2elfie", typeof(AggregateException))]
        [InlineData("catalog2elfie -source -storageBaseAddress file:///C:/NuGet -storageType file -storagePath C:\\NuGet -maxthreads 1 -verbose true", typeof(ArgumentOutOfRangeException))]
        [InlineData("catalog2elfie -storageBaseAddress file:///C:/NuGet -storageType file -storagePath C:\\NuGet -maxthreads 1 -verbose true", typeof(AggregateException))]
        [InlineData("catalog2elfie -source http://api.nuget.org/v3/catalog0/index.json -storageType file -storagePath C:\\NuGet -maxthreads 1 -verbose true", typeof(AggregateException))]
        [InlineData("catalog2elfie -source http://api.nuget.org/v3/catalog0/index.json -storageBaseAddress file:///C:/NuGet -storagePath C:\\NuGet -maxthreads 1 -verbose true", typeof(AggregateException))]
        [InlineData("catalog2elfie -source http://api.nuget.org/v3/catalog0/index.json -storageBaseAddress file:///C:/NuGet -storageType file -maxthreads 1 -verbose true", typeof(AggregateException))]
        [InlineData("catalog2elfie -source http://api.nuget.org/v3/catalog0/index.json -storageBaseAddress file:///C:/NuGet -storageType file -storagePath C:\\NuGet -maxthreads 1 -interval -1 -verbose true", typeof(AggregateException))]
        public void Validate_InvalidValues(string inputArgs, Type expectedException)
        {
            // Arrange
            string[] args = inputArgs?.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Act
            Action action = delegate
            {
                Catalog2ElfieOptions options = Catalog2ElfieOptions.FromArgs(args);
            };

            // Assert
            Assert.Throws(expectedException, action);
        }
    }
}
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
    public class CommandHelpersTests
    {
        [Theory]
        [InlineData("1", 1)]
        [InlineData("1234", 1234)]
        public void GetMaxThreads_ValidValues(string inputValue, int expectedValue)
        {
            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments.Add("-maxthreads", inputValue);

            // Act
            int maxThreads = CommandHelpers.GetMaxThreads(arguments);

            // Assert
            Assert.Equal(expectedValue, maxThreads);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("-1234")]
        [InlineData("-thenextparameter")]
        [InlineData("one")]
        public void GetMaxThreads_InvalidValues(string inputValue)
        {
            int expectedValue = Environment.ProcessorCount;

            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments.Add("-maxthreads", inputValue);

            // Act
            int maxThreads = CommandHelpers.GetMaxThreads(arguments);

            // Assert
            Assert.Equal(expectedValue, maxThreads);
        }
    }
}
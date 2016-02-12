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
            arguments.Add("-maxThreads", inputValue);

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
            arguments.Add("-maxThreads", inputValue);

            // Act
            int maxThreads = CommandHelpers.GetMaxThreads(arguments);

            // Assert
            Assert.Equal(expectedValue, maxThreads);
        }

        [Theory]
        [InlineData("1.1", "1.1")]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2.3.4", "1.2.3.4")]
        public void GetIndexerVersion_ValidValues(string inputVersion, string expectedVersion)
        {
            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments.Add("-indexerVersion", inputVersion);

            // Act
            Version version = CommandHelpers.GetIndexerVersion(arguments);

            // Assert
            Assert.True(version.ToString().Equals(expectedVersion));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("1.")]
        [InlineData("1.2.3.4.5")]
        [InlineData("1.2.3-prerelease")]
        public void GetIndexerVersion_InvalidValues(string inputVersion)
        {
            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments.Add("-indexerVersion", inputVersion);

            // Act
            Version version = CommandHelpers.GetIndexerVersion(arguments);

            // Assert
            Assert.Null(version);
        }

        [Fact]
        public void GetTempPath_ValidValue()
        {
            // Arrange
            string tempPath = @"C:\Temp";
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments.Add("-tempPath", tempPath);

            // Act
            string path = CommandHelpers.GetTempPath(arguments);

            // Assert
            Assert.Equal(path, tempPath);
        }

        [Fact]
        public void GetTempPath_InvalidValue()
        {
            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments.Add("-tempPath", "?");

            // Act
            Action action = delegate { CommandHelpers.GetTempPath(arguments); };

            // Assert
            Assert.Throws(typeof(ArgumentException), action);
        }

        [Fact]
        public void GetTempPath_Unspecified()
        {
            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>();

            // Act
            string path = CommandHelpers.GetTempPath(arguments);

            // Assert
            Assert.Equal(path, Path.GetTempPath());
        }
    }
}
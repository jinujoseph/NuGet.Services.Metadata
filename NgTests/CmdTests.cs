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
    public class CmdTests
    {
        [Fact]
        public void Quiet_Echo()
        {
            // Arrange

            // Act
            Cmd c = Cmd.Quiet("cmd.exe", "/C ECHO Done.", TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(c.HasExited);
            Assert.True(c.ExitCode.Equals(0));
        }

        [Fact]
        public void Echo_Echo()
        {
            // Arrange

            // Act
            Cmd c = Cmd.Echo("cmd.exe", "/C ECHO Done.", TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(c.HasExited);
            Assert.True(c.ExitCode.Equals(0));
        }

        [Fact]
        public void Quiet_FileRedirect()
        {

            // Arrange
            string logFile = Path.GetTempFileName();

            // Act
            try
            {
                Cmd c = Cmd.Quiet("cmd.exe", "/C ECHO Working...& ECHO Done.", TimeSpan.FromSeconds(1), outputFilePath: logFile);

                // Assert
                Assert.True(c.HasExited);
                Assert.True(c.ExitCode.Equals(0));
                Assert.True(File.Exists(logFile));
                string text = File.ReadAllText(logFile);
                Assert.True(text.Equals("Working...\r\nDone.\r\n"));
            }
            finally
            {
                File.Delete(logFile);
            }
        }
    }
}
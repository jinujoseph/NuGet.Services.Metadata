// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class ReplacementFilterTests
    {
        [Theory]
        [MemberData("TokenizingReplacesTheCorrectStringsData")]
        public void TokenizingReplacesTheCorrectStrings(string text, string oldValue, string newValue, TokenAttributes expected)
        {
            // arrange
            var tokenStream = new KeywordTokenizer(new StringReader(text));
            var filter = new ReplacementFilter(tokenStream, oldValue, newValue);

            // act
            var actual = filter.Tokenize().ToArray();

            // assert
            Assert.Equal(new[] { expected }, actual);
        }

        public static IEnumerable<object[]> TokenizingReplacesTheCorrectStringsData
        {
            get
            {
                // identity
                yield return new object[]
                {
                    "DotNetZip",
                    "foo",
                    "bar",
                    new TokenAttributes("DotNetZip", 0, 9)
                };

                // single character old value
                yield return new object[]
                {
                    "Dot Net Zip",
                    " ",
                    ".",
                    new TokenAttributes("Dot.Net.Zip", 0, 11)
                };

                // multiple character old value depth two with three terms
                yield return new object[]
                {
                    "Dot  Net  Zip",
                    "  ",
                    ".",
                    new TokenAttributes("Dot.Net.Zip", 0, 13)
                };

                // empty new value
                yield return new object[]
                {
                    "Dot Net Zip",
                    " ",
                    string.Empty,
                    new TokenAttributes("DotNetZip", 0, 11)
                };
            }
        }
    }
}

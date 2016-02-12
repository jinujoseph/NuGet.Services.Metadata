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
    public class RegistrationIndexTests
    {
        [Fact]
        public void Deserialize_ValidJsonFile()
        {
            // Arrange
            string inputJson = this.GetRegistrationJson(true, "", true, "-prerelease", false, "", false, "-beta1");

            // Act
            RegistrationIndex item = RegistrationIndex.Deserialize(inputJson);

            // Assert
            Assert.True(item.Items.Length == 2);
            Assert.True(item.Items[0].Items.Length == 2);
            Assert.True(item.Items[0].Items[0].CatalogEntry.Listed);
            Assert.True(item.Items[0].Items[0].CatalogEntry.PackageVersion.Equals("7.1.0"));
            Assert.True(item.Items[0].Items[1].CatalogEntry.Listed);
            Assert.True(item.Items[0].Items[1].CatalogEntry.PackageVersion.Equals("7.2.0-prerelease"));
            Assert.True(item.Items[1].Items.Length == 2);
            Assert.False(item.Items[1].Items[0].CatalogEntry.Listed);
            Assert.True(item.Items[1].Items[0].CatalogEntry.PackageVersion.Equals("8.0.1"));
            Assert.False(item.Items[1].Items[1].CatalogEntry.Listed);
            Assert.True(item.Items[1].Items[1].CatalogEntry.PackageVersion.Equals("8.0.2-beta1"));
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
                RegistrationIndex item = RegistrationIndex.Deserialize(inputJson);
            };

            // Assert
            Assert.Throws(expectedException, action);
        }

        [Theory]
        [InlineData(true, "", true, "", true, "", true, "", "8.0.2")]
        [InlineData(true, "", true, "", true, "", false, "", "8.0.1")]
        [InlineData(true, "", true, "", false, "", false, "", "7.2.0")]
        [InlineData(true, "", true, "", true, "", true, "-test", "8.0.1")]
        [InlineData(true, "", true, "-alpha1", true, "-beta1", true, "-beta2", "7.1.0")]
        [InlineData(true, "", true, "-prerelease", false, "", false, "-beta1", "7.1.0")]
        [InlineData(false, "", false, "", false, "", false, "", null)]
        [InlineData(true, "-a", true, "-b", true, "-c", true, "-d", null)]
        public void GetLatestStableVersion_ValidJsonFile(bool listed0, string prerelease0, bool listed1, string prerelease1, bool listed2, string prerelease2, bool listed3, string prerelease3, string expectedLatestStableVersion)
        {
            // Arrange
            string inputJson = this.GetRegistrationJson(listed0, prerelease0, listed1, prerelease1, listed2, prerelease2, listed3, prerelease3);

            // Act
            RegistrationIndex item = RegistrationIndex.Deserialize(inputJson);
            RegistrationIndexPackage package = item.GetLatestStableVersion();

            // Assert
            if (expectedLatestStableVersion == null)
            {
                Assert.Null(package);
            }
            else
            {
                Assert.True(package.CatalogEntry.PackageVersion.Equals(expectedLatestStableVersion));
            }
        }

        string GetRegistrationJson(bool listed0, string prerelease0, bool listed1, string prerelease1, bool listed2, string prerelease2, bool listed3, string prerelease3)
        {
            return $@"{{
                      ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json"",
                      ""@type"": [
                        ""catalog:CatalogRoot"",
                        ""PackageRegistration"",
                      ],
                      ""commitId"": ""521c88c2-7b19-428c-b7fb-fd35478be120"",
                      ""commitTimeStamp"": ""2016-01-09T01:07:44.074772Z"",
                      ""count"": 2,
                      ""items"": [
                        {{
                          ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json#page/7.1.0/7.2.0"",
                          ""@type"": ""catalog:CatalogPage"",
                          ""commitId"": ""521c88c2-7b19-428c-b7fb-fd35478be120"",
                          ""commitTimeStamp"": ""2016-01-09T01:07:44.074772Z"",
                          ""count"": 2,
                          ""items"": [
                           {{
                              ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/7.1.0.json"",
                              ""@type"": ""Package"",
                              ""commitId"": ""521c88c2-7b19-428c-b7fb-fd35478be120"",
                              ""commitTimeStamp"": ""2016-01-09T01:07:44.074772Z"",
                              ""catalogEntry"": {{
                                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.12.29.01.03.58/newtonsoft.json.7.1.0.json"",
                                ""@type"": ""PackageDetails"",
                                ""id"": ""Newtonsoft.Json"",
                                ""listed"": {listed0.ToString().ToLower()},
                                ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.7.1.0.nupkg"",
                                ""version"": ""7.1.0{prerelease0}""
                              }},
                              ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.7.1.0.nupkg"",
                              ""registration"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json""
                            }},
                            {{
                              ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/7.2.0.json"",
                              ""@type"": ""Package"",
                              ""commitId"": ""521c88c2-7b19-428c-b7fb-fd35478be120"",
                              ""commitTimeStamp"": ""2016-01-09T01:07:44.074772Z"",
                              ""catalogEntry"": {{
                                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2016.01.09.01.07.08/newtonsoft.json.7.2.0.json"",
                                ""@type"": ""PackageDetails"",
                                ""id"": ""Newtonsoft.Json"",
                                ""listed"": {listed1.ToString().ToLower()},
                                ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.7.2.0.nupkg"",
                                ""version"": ""7.2.0{prerelease1}""
                              }},
                              ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.7.2.0.nupkg"",
                              ""registration"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json""
                            }}
                          ],
                          ""parent"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json"",
                          ""lower"": ""7.1.0"",
                          ""upper"": ""7.2.0""
                        }},
                        {{
                          ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json#page/8.0.1/8.0.2"",
                          ""@type"": ""catalog:CatalogPage"",
                          ""commitId"": ""521c88c2-7b19-428c-b7fb-fd35478be120"",
                          ""commitTimeStamp"": ""2016-01-09T01:07:44.074772Z"",
                          ""count"": 2,
                          ""items"": [
                           {{
                              ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/8.0.1.json"",
                              ""@type"": ""Package"",
                              ""commitId"": ""521c88c2-7b19-428c-b7fb-fd35478be120"",
                              ""commitTimeStamp"": ""2016-01-09T01:07:44.074772Z"",
                              ""catalogEntry"": {{
                                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.12.29.01.03.58/newtonsoft.json.8.0.1.json"",
                                ""@type"": ""PackageDetails"",
                                ""id"": ""Newtonsoft.Json"",
                                ""listed"": {listed2.ToString().ToLower()},
                                ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.8.0.1.nupkg"",
                                ""version"": ""8.0.1{prerelease2}""
                              }},
                              ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.8.0.1.nupkg"",
                              ""registration"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json""
                            }},
                            {{
                              ""@id"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/8.0.2.json"",
                              ""@type"": ""Package"",
                              ""commitId"": ""521c88c2-7b19-428c-b7fb-fd35478be120"",
                              ""commitTimeStamp"": ""2016-01-09T01:07:44.074772Z"",
                              ""catalogEntry"": {{
                                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2016.01.09.01.07.08/newtonsoft.json.8.0.2.json"",
                                ""@type"": ""PackageDetails"",
                                ""id"": ""Newtonsoft.Json"",
                                ""listed"": {listed3.ToString().ToLower()},
                                ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.8.0.2.nupkg"",
                                ""version"": ""8.0.2{prerelease3}""
                              }},
                              ""packageContent"": ""https://api.nuget.org/packages/newtonsoft.json.8.0.2.nupkg"",
                              ""registration"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json""
                            }}
                          ],
                          ""parent"": ""https://api.nuget.org/v3/registration1/newtonsoft.json/index.json"",
                          ""lower"": ""8.0.1"",
                          ""upper"": ""8.0.2""
                        }}],
                      ""@context"": {{
                        ""@vocab"": ""http://schema.nuget.org/schema#"",
                        ""catalog"": ""http://schema.nuget.org/catalog#"",
                        ""xsd"": ""http://www.w3.org/2001/XMLSchema#"",
                        ""items"": {{
                          ""@id"": ""catalog:item"",
                          ""@container"": ""@set""
                        }},
                        ""commitTimeStamp"": {{
                          ""@id"": ""catalog:commitTimeStamp"",
                          ""@type"": ""xsd:dateTime""
                        }},
                        ""commitId"": {{
                          ""@id"": ""catalog:commitId""
                        }},
                        ""count"": {{
                          ""@id"": ""catalog:count""
                        }},
                        ""parent"": {{
                          ""@id"": ""catalog:parent"",
                          ""@type"": ""@id""
                        }},
                        ""tags"": {{
                          ""@container"": ""@set"",
                          ""@id"": ""tag""
                        }},
                        ""packageTargetFrameworks"": {{
                          ""@container"": ""@set"",
                          ""@id"": ""packageTargetFramework""
                        }},
                        ""dependencyGroups"": {{
                          ""@container"": ""@set"",
                          ""@id"": ""dependencyGroup""
                        }},
                        ""dependencies"": {{
                          ""@container"": ""@set"",
                          ""@id"": ""dependency""
                        }},
                        ""packageContent"": {{
                          ""@type"": ""@id""
                        }},
                        ""published"": {{
                          ""@type"": ""xsd:dateTime""
                        }},
                        ""registration"": {{
                          ""@type"": ""@id""
                        }}
                      }}
                    }}";
        }
    }
}
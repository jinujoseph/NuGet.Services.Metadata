// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Catalog = NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    /// <summary>
    /// Extension methods for the IStorage types
    /// </summary>
    static class IStorageExtensions
    {
        /// <summary>
        /// Composes the storage resource URL for a package. 
        /// </summary>
        /// <param name="packageId">The id of the package.</param>
        /// <param name="packageVersion">The version of the package</param>
        /// <param name="filename">The file name to use in the resource URI.</param>
        /// <returns>The anticipated storage URL for the package.</returns>
        public static Uri ComposePackageResourceUrl(this IStorage storage, string packageId, string packageVersion, string filename)
        {
            // The resource URI should look similar to this file:///C:/NuGet//Packages/Autofac.Mvc2/2.3.2.632/autofac.mvc2.2.3.2.632.nupkg

            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentNullException(packageId);
            }

            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentNullException(packageVersion);
            }

            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(filename);
            }

            // Clean the strings so they don't contain any invalid path chars.
            packageId = ReplaceInvalidPathChars(packageId);
            packageVersion = ReplaceInvalidPathChars(packageVersion);
            filename = ReplaceInvalidPathChars(filename);

            string relativePath = $"packages/{packageId}/{packageVersion}/{filename}";
            relativePath = relativePath.ToLowerInvariant();

            Uri resourceUri = new Uri(storage.BaseAddress, relativePath);
            return resourceUri;
        }

        public static Uri ComposeIdxResourceUrl(this IStorage storage, Version toolVersion, string packageId, string packageVersion)
        {
            // The resource URI should look similar to this file:///C:/NuGet//idx/1.0/Autofac.Mvc2/2.3.2.632/autofac.mvc2.2.3.2.632.idx

            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentNullException("packageId");
            }

            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentNullException("packageVersion");
            }

            if (toolVersion == null)
            {
                throw new ArgumentNullException("toolVersion");
            }

            // Clean the strings so they don't contain any invalid path chars.
            packageId = ReplaceInvalidPathChars(packageId);
            packageVersion = ReplaceInvalidPathChars(packageVersion);

            string relativeFilePath = $"{packageId}/{packageVersion}/{packageId} {packageVersion}.idx";
            relativeFilePath = relativeFilePath.ToLowerInvariant();

            Uri resourceUri = storage.ComposeIdxResourceUrl(toolVersion, relativeFilePath);
            return resourceUri;
        }

        public static Uri ComposeIdxResourceUrl(this IStorage storage, Version toolVersion, string relativeFilePath)
        {
            // The resource URI should look similar to this file:///C:/NuGet//idx/1.0/Autofac.Mvc2/2.3.2.632/autofac.mvc2.2.3.2.632.idx

            if (string.IsNullOrWhiteSpace(relativeFilePath))
            {
                throw new ArgumentNullException("relativeFilePath");
            }

            if (toolVersion == null)
            {
                throw new ArgumentNullException("toolVersion");
            }

            string relativePath = $"idx/{toolVersion.Major}.{toolVersion.Minor}/{relativeFilePath}";
            relativePath = relativePath.ToLowerInvariant();

            Uri resourceUri = new Uri(storage.BaseAddress, relativePath);
            return resourceUri;
        }

        /// <summary>
        /// Saves the contents of a URL to storage.
        /// </summary>
        /// <param name="sourceUrl">The URL to download the contents from.</param>
        /// <param name="destinationResourceUrl">The resource URL to save the contents to.</param>
        public static void SaveUrlContents(this IStorage storage, Uri sourceUrl, Uri destinationResourceUrl)
        {
            using (Catalog.CollectorHttpClient client = new Catalog.CollectorHttpClient())
            {
                using (Stream downloadStream = client.GetStreamAsync(sourceUrl).Result)
                {
                    using (StreamStorageContent packageStorageContent = new StreamStorageContent(downloadStream))
                    {
                        storage.Save(destinationResourceUrl, packageStorageContent, new CancellationToken()).Wait();
                    }
                }
            }
        }

        /// <summary>
        /// Replaces the invalid file or path characters with a hyphen '-'.
        /// </summary>
        private static string ReplaceInvalidPathChars(string inputText)
        {
            if (String.IsNullOrWhiteSpace(inputText))
            {
                return inputText;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            IEnumerable<char> newChars = inputText.Select(c => invalidChars.Contains(c) ? '-' : c);

            return new String(newChars.ToArray());
        }
    }
}
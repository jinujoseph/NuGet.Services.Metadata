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
using System.Net;

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

        /// <summary>
        /// Composes the storage resource URL for an idx file.
        /// </summary>
        /// <param name="toolVersion">The version of the Elfie toolset.</param>
        /// <param name="packageId">The id of the package.</param>
        /// <param name="packageVersion">The version of the package</param>
        /// <returns>The idx storage URL to the package.</returns>
        /// <remarks>The idx storage URL is structured as follows. {storageroot}/idx/{toolversion}/{packageId}/{packageversion}/{idx file name}</remarks>
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

        /// <summary>
        /// Composes the storage resource URL for a file in the idx storage.
        /// </summary>
        /// <param name="toolVersion">The version of the Elfie toolset.</param>
        /// <param name="relativeFilePath">The relative file path, including filename, of the resource.</param>
        /// <returns>The idx storage URL to the relativeFilePath.</returns>
        /// <remarks>The idx storage URL is structured as follows. {storageroot}/idx/{toolversion}/{relativeFilePath}</remarks>
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
        /// Composes the storage resource URL for a file in the ardb storage.  
        /// </summary>  
        /// <param name="toolVersion">The version of the Elfie toolset.</param>  
        /// <param name="relativeFilePath">The relative file path, including filename, of the resource.</param>  
        /// <returns>The ardb storage URL to the relativeFilePath.</returns>  
        /// <remarks>The ardb storage URL is structured as follows. {storageroot}/ardb/{toolversion}/{relativeFilePath}</remarks>  
        public static Uri ComposeArdbResourceUrl(this IStorage storage, Version toolVersion, string relativeFilePath)
        {
            // The resource URI should look similar to this file:///C:/NuGet//ardb/1.0/20160209.0/20160209.0.ardb.txt  

            if (string.IsNullOrWhiteSpace(relativeFilePath))
            {
                throw new ArgumentNullException("relativeFilePath");
            }

            if (toolVersion == null)
            {
                throw new ArgumentNullException("toolVersion");
            }

            string relativePath = $"ardb/{toolVersion.Major}.{toolVersion.Minor}/{relativeFilePath}";
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
            using (WebClient client = new WebClient())
            {
                using (Stream downloadStream = client.OpenRead(sourceUrl))
                {
                    using (StreamStorageContent packageStorageContent = new StreamStorageContent(downloadStream))
                    {
                        storage.Save(destinationResourceUrl, packageStorageContent, new CancellationToken()).Wait();
                    }
                }
            }
        }

        /// <summary>
        /// Saves the contents of a file to storage.
        /// </summary>
        /// <param name="sourceFile">The file to save.</param>
        /// <param name="destinationResourceUrl">The resource URL to save the contents to.</param>
        public static void SaveFileContents(this IStorage storage, string sourceFile, Uri destinationResourceUrl)
        {
            using (FileStream fileStream = File.OpenRead(sourceFile))
            {
                using (StreamStorageContent packageStorageContent = new StreamStorageContent(fileStream))
                {
                    storage.Save(destinationResourceUrl, packageStorageContent, new CancellationToken()).Wait();
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
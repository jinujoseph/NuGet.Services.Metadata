﻿using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using VDS.RDF;

namespace Ng
{
    //BUG:  we really want to order-by the LastEdited (in the SortedDictionary) but include the Created in the data (as it is the published date)

    public static class Feed2Catalog
    {
        static Uri MakeCreatedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=Created gt DateTime'{1}'&$top={2}&$orderby=Created&$select=Created",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        static Uri MakeLastEditedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=LastEdited gt DateTime'{1}'&$top={2}&$orderby=LastEdited&$select=LastEdited",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        public static Task<SortedList<DateTime, IList<Uri>>> GetCreatedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeCreatedUri(source, since, top));
        }

        public static Task<SortedList<DateTime, IList<Uri>>> GetEditedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeLastEditedUri(source, since, top));
        }

        public static async Task<SortedList<DateTime, IList<Uri>>> GetPackages(HttpClient client, Uri uri)
        {
            SortedList<DateTime, IList<Uri>> result = new SortedList<DateTime, IList<Uri>>();

            XElement feed;
            using (Stream stream = await client.GetStreamAsync(uri))
            {
                feed = XElement.Load(stream);
            }

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace dataservices = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace metadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

            foreach (XElement entry in feed.Elements(atom + "entry"))
            {
                Uri content = new Uri(entry.Element(atom + "content").Attribute("src").Value);
                DateTime date = DateTime.Parse(entry.Element(metadata + "properties").Element(dataservices + "Created").Value);

                IList<Uri> contentUris;
                if (!result.TryGetValue(date, out contentUris))
                {
                    contentUris = new List<Uri>();
                    result.Add(date, contentUris);
                }

                contentUris.Add(content);
            }

            return result;
        }

        static async Task DownloadMetadata2Catalog(HttpClient client, SortedList<DateTime, IList<Uri>> packages, Storage storage)
        {
            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            DateTime lastCreated = DateTime.MinValue;

            foreach (KeyValuePair<DateTime, IList<Uri>> entry in packages)
            {
                foreach (Uri uri in entry.Value)
                {
                    HttpResponseMessage response = await client.GetAsync(uri);

                    if (response.IsSuccessStatusCode)
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        {
                            CatalogItem item = Utils.CreateCatalogItem(stream, entry.Key, null, uri.ToString());

                            if (item != null)
                            {
                                writer.Add(item);

                                Trace.TraceInformation("Add: {0}", uri);
                            }
                            else
                            {
                                Trace.TraceWarning("Unable to extract metadata from: {0}", uri);
                            }
                        }
                    }
                    else
                    {
                        Trace.TraceWarning("Unable to download: {0}", uri);
                    }
                }

                lastCreated = entry.Key;
            }

            IGraph commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, lastCreated, null);

            await writer.Commit(commitMetadata);

            Trace.TraceInformation("COMMIT");
        }

        static async Task<DateTime> GetCatalogProperty(Storage storage, string propertyName)
        {
            string json = await storage.LoadString(storage.ResolveUri("index.json"));

            if (json != null)
            {
                JObject obj = JObject.Parse(json);

                JToken token;
                if (obj.TryGetValue(propertyName, out token))
                {
                    return token.ToObject<DateTime>();
                }
            }

            return DateTime.MinValue.ToUniversalTime();
        }

        static async Task Loop(string source, StorageFactory storageFactory)
        {
            Storage storage = storageFactory.Create();

            const string LastCreated = "nuget:lastCreated";
            const string LastEdited = "nuget:lastEdited";

            int top = 20;
            int timeout = 300;

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(timeout);

                //  fetch and add all newly CREATED packages - in order

                SortedList<DateTime, IList<Uri>> createdPackages;
                do
                {
                    DateTime lastCreated = await GetCatalogProperty(storage, LastCreated);
                    Trace.TraceInformation("CATALOG LastCreated: {0}", lastCreated.ToString("O"));

                    createdPackages = await GetCreatedPackages(client, source, lastCreated, top);
                    Trace.TraceInformation("FEED CreatedPackages: {0}", createdPackages.Count);

                    await DownloadMetadata2Catalog(client, createdPackages, storage);
                }
                while (createdPackages.Count > 0);

                //  THEN fetch and add all EDITED packages - in order

                SortedList<DateTime, IList<Uri>> editedPackages;
                do
                {
                    DateTime lastEdited = await GetCatalogProperty(storage, LastEdited);
                    Trace.TraceInformation("CATALOG LastEdited: {0}", lastEdited.ToString("O"));

                    editedPackages = await GetEditedPackages(client, source, lastEdited, top);
                    Trace.TraceInformation("FEED EditedPackages: {0}", editedPackages.Count);

                    await DownloadMetadata2Catalog(client, editedPackages, storage);
                }
                while (editedPackages.Count > 0);
            }
        }

        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null)
            {
                return;
            }

            string source = CommandHelpers.GetSource(arguments);
            if (source == null)
            {
                return;
            }

            StorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments);
            if (storageFactory == null)
            {
                return;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" storage: \"{1}\"", source, storageFactory);

            Loop(source, storageFactory).Wait();
        }
    }
}
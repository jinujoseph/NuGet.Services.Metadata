using Nest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES = NuGet.Search.Common.ElasticSearch;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace NuGet.Search.Common.ElasticSearch
{
    public class ElasticSearchClient
    {
        const string INDEXEDITEM = "INDEXED_ITEM";
        public ElasticClient Client { get; set; }

        public ElasticSearchClient(string serverUrl, string defaultIndex, int timeout = 300000)
        {
            this.Init(serverUrl, defaultIndex, timeout);
        }

        private void Init(string serverUrl, string defaultIndex, int timeout = 300000)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new ArgumentNullException("serverUrl");
            }

            if (string.IsNullOrWhiteSpace(defaultIndex))
            {
                throw new ArgumentNullException("defaultIndex");
            }

            Uri serverUri = new Uri(serverUrl);
            var connectionSettings = new ConnectionSettings(serverUri);
            connectionSettings.RequestTimeout(TimeSpan.FromMilliseconds(timeout)); // 5 minutes
            connectionSettings.DefaultIndex(defaultIndex);

            this.Client = new ElasticClient(connectionSettings);

            this.CreateIndexIfNotExists(defaultIndex);
        }

        public void Index<TDocumentClass>(TDocumentClass document)
            where TDocumentClass : class, IIndexDocument
        {
            this.Client.Index(document);
        }

        public void Index<TDocumentClass>(IEnumerable<TDocumentClass> documents)
            where TDocumentClass : class, IIndexDocument
        {
            this.Client.IndexMany(documents);
        }

        public TDocumentClass GetDocument<TDocumentClass>(string documentId, string docType = null)
            where TDocumentClass : class, IIndexDocument, new()
        {
            ISearchResponse<TDocumentClass> response;

            JObject query = new JObject();
            query["query"] = new JObject();
            query["query"]["text"] = new JObject();
            query["query"]["text"]["_id"] = documentId;
            string queryString = JsonConvert.SerializeObject(query);

            if (string.IsNullOrWhiteSpace(docType))
            {
                response = this.Client.Search<TDocumentClass>(s => s.Query(q => q.Raw(queryString)));
            }
            else
            {
                response = this.Client.Search<TDocumentClass>(s => s.Type(docType).Query(q => q.Raw(queryString)));
            }

            int count = response.Hits.Count();
            if (count > 1)
            {
                throw new Exception("More than one document was found for documentId: " + documentId + ", " + count);
            }

            if (count == 0)
            {
                return null;
            }

            return response.Hits.ElementAt(0).Source;
        }

        public bool DoesIndexContainDocument(string documentId, string documentIdName)
        {
            var response = this.Client.Search<IIndexDocument>(s => s.Query(q => q.QueryString(d => d.Query(documentIdName + ":\"" + documentId + "\""))));

            return response.Hits.Count() > 0;
        }

        public bool DoesIndexContainDocument<TDocumentClass>(string documentId, string docType = null)
            where TDocumentClass : class, IIndexDocument, new()
        {
            ISearchResponse<TDocumentClass> response;

            JObject query = new JObject();
            query["query"] = new JObject();
            query["query"]["text"] = new JObject();
            query["query"]["text"]["_id"] = documentId;
            string queryString = JsonConvert.SerializeObject(query);

            if (string.IsNullOrWhiteSpace(docType))
            {
                response = this.Client.Search<TDocumentClass>(s => s.Query(q => q.Raw(queryString)));
            }
            else
            {
                response = this.Client.Search<TDocumentClass>(s => s.Type(docType).Query(q => q.Raw(queryString)));
            }

            return response.Hits.Count() > 0;
        }

        private void CreateIndexIfNotExists(string indexName)
        {
            if (this.Client.IndexExists(indexName).Exists)
            {
                return;
            }

            IndexSettings indexSettings = new IndexSettings();
            indexSettings.NumberOfShards = 12;
            indexSettings.NumberOfReplicas = 0;

            indexSettings.RefreshInterval = "30s";

            var hashFilter = new WordDelimiterTokenFilter();
            hashFilter.TypeTable = new string[] { "# => ALPHA" };
            indexSettings.Analysis.TokenFilters.Add("hashFilter", hashFilter);
            var hashAnalyzer = new CustomAnalyzer();
            hashAnalyzer.Tokenizer = "whitespace";
            hashAnalyzer.Filter = new string[] { "hashFilter" };
            indexSettings.Analysis.Analyzers.Add("hashAnalyzer", hashAnalyzer);

            indexSettings.Analysis.Tokenizers.Add("nGram", new NGramTokenizer() { MinGram = 3, MaxGram = 10 });
            var ngramAnalyzer = new CustomAnalyzer();
            ngramAnalyzer.Tokenizer = "nGram";
            ngramAnalyzer.Filter = new string[] { "standard", "lowercase", "asciifolding" };

            CustomAnalyzer lowercaseKeywordAnalyzer = new CustomAnalyzer();
            lowercaseKeywordAnalyzer.Tokenizer = "standard";
            lowercaseKeywordAnalyzer.Filter = new string[] { "lowercase" };

            indexSettings.Analysis.Analyzers.Add("ngramAnalyzer", ngramAnalyzer);
            indexSettings.Analysis.Analyzers.Add("lowercaseKeywordAnalyzer", lowercaseKeywordAnalyzer);

            CreateIndexRequest createIndexRequest = new CreateIndexRequest(indexName);
            createIndexRequest.Settings = indexSettings;
            var indexCreationResponse = this.Client.CreateIndex(createIndexRequest);
        }
    }
}

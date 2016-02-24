using Kendo.Mvc.UI;
using NuGet.Search.Website.Models;
using NuGet.Search.Common;
using NuGet.Search.Common.ElasticSearch;
using ES = NuGet.Search.Common.ElasticSearch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using NuGet.Search.Common.ElasticSearch.Sarif;
using Nest;

namespace NuGet.Search.Website.Controllers
{
    public class SearchController : ApiController
    {
        public readonly string[] SearchFields = new string[] {
            "id",
            "runLogs.runInfo.machine",
            "runLogs.runInfo.runDate",
            "runLogs.toolInfo.fullName",
        };

        [Route("api/Search")]
        [HttpPost]
        public DataSourceResult Search(SearchParams searchParams)
        {
            if (searchParams == null)
            {
                throw new ArgumentNullException("Search parameters were not parsed on the server.");
            }

            searchParams.searchText = Uri.UnescapeDataString(searchParams.searchText);
            searchParams.sourceInclude = string.Join(",", this.SearchFields);

            string elasticSearchResponse = this.SearchElasticSearch(searchParams);
            JObject elasticSearchJson = (JObject)JsonConvert.DeserializeObject(elasticSearchResponse);
            JArray resultHitsJson = (JArray)elasticSearchJson["hits"]["hits"];

            DataSourceResult result = new DataSourceResult();

            result.Total = (int)elasticSearchJson["hits"]["total"];
            result.Data = resultHitsJson.Select(s =>
            {
                JObject source = (JObject)s["_source"];

                ResultLog row = source.ToObject<ResultLog>();
                return source.ToObject<ResultLog>();
            });

            return result;
        }

        [Route("api/SearchRaw")]
        [HttpPost]
        public string SearchRaw(SearchParams searchParams)
        {
            return this.SearchElasticSearch(searchParams);
        }

        [Route("api/Documents/GetJson/{index}/{documentId}")]
        [HttpGet]
        public string GetDocumentJson(string index, string documentId)
        {
            SearchParams searchParams = new SearchParams();
            searchParams.index = index;
            searchParams.searchText = "_id:" + documentId;
            searchParams.pageSize = "1";
            searchParams.page = "1";

            string elasticSearchResponse = this.SearchElasticSearch(searchParams);
            JObject elasticSearchJson = (JObject)JsonConvert.DeserializeObject(elasticSearchResponse);
            JArray documentJson = (JArray)elasticSearchJson["hits"]["hits"];

            if (documentJson.Count > 1)
            {
                throw new IndexOutOfRangeException("More than 1 document record was found.");
            }

            if (documentJson.Count < 1)
            {
                throw new IndexOutOfRangeException("An indexed record was not found.");
            }

            JObject sarifJson = (JObject)documentJson[0]["_source"];

            if (sarifJson == null)
            {
                throw new ArgumentNullException("The record did not contain the document's json.");
            }

            if (sarifJson["runLogs"] != null)
            {
                JToken runLogs = sarifJson["runLogs"];
                if (runLogs[0]["results"] != null)
                {
                    JArray results = runLogs[0]["results"] as JArray;

                    for (int i = 0; i < results.Count; i++)
                    {
                        JToken result = results[i];
                        if (result["shortMessage"] != null)
                        {
                            JToken shortMessage = result["shortMessage"];
                            string message = shortMessage.Value<string>();
                            message = HttpUtility.HtmlEncode(message);
                            message = message.Replace(" ", "&nbsp;");
                            message = message.Replace("\t", "&nbsp;&nbsp;&nbsp;&nbsp;");
                            message = message.Replace("\n", "<br />");
                            result["shortMessage"] = message;
                        }
                        if (result["fullMessage"] != null)
                        {
                            JToken shortMessage = result["fullMessage"];
                            string message = shortMessage.Value<string>();
                            message = HttpUtility.HtmlEncode(message);
                            message = message.Replace(" ", "&nbsp;");
                            message = message.Replace("\t", "&nbsp;&nbsp;&nbsp;&nbsp;");
                            message = message.Replace("\n", "<br />");
                            result["fullMessage"] = message;
                        }
                        if (result["properties"] != null)
                        {
                            JToken properties = result["properties"];
                            if (properties["Details"] != null)
                            {
                                JToken shortMessage = properties["Details"];
                                string message = shortMessage.Value<string>();
                                message = HttpUtility.HtmlEncode(message);
                                message = message.Replace(" ", "&nbsp;");
                                message = message.Replace("\t", "&nbsp;&nbsp;&nbsp;&nbsp;");
                                message = message.Replace("\n", "<br />");
                                properties["Details"] = message;
                            }
                        }
                    }
                }
            }

            return sarifJson.ToString();
        }

        private string SearchElasticSearch(SearchParams searchParams)
        {
            string url = System.Configuration.ConfigurationManager.AppSettings["ElasticSearchUrl"];
            string query = searchParams.searchText;

            if (String.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException("url");
            }

            if (String.IsNullOrWhiteSpace(searchParams.index))
            {
                throw new ArgumentNullException("index");
            }

            if (String.IsNullOrWhiteSpace(query))
            {
                query = "*";
            }

            string pretty = String.Empty;
#if DEBUG
            pretty = "pretty=1&";
#endif

            string searchUri = url + "/" + searchParams.index + "/_search?" + pretty + "q=" + System.Web.HttpUtility.UrlEncode(query);

            if (!String.IsNullOrWhiteSpace(searchParams.sort))
            {
                searchUri = searchUri + "&sort=" + System.Web.HttpUtility.UrlEncode(searchParams.sort);
            }

            if (!String.IsNullOrWhiteSpace(searchParams.pageSize))
            {
                int size;
                if (Int32.TryParse(searchParams.pageSize, out size))
                {
                    searchUri = searchUri + "&size=" + size;

                    if (!String.IsNullOrWhiteSpace(searchParams.page))
                    {
                        int pageNumber;
                        if (Int32.TryParse(searchParams.page, out pageNumber))
                        {
                            // BUGBUG: Page size is one based?
                            int from = size * (pageNumber - 1);

                            if (from >= 0)
                            {
                                searchUri = searchUri + "&from=" + from;
                            }
                        }
                    }
                }
            }

            if (!String.IsNullOrWhiteSpace(searchParams.sourceInclude))
            {
                searchUri += "&_source_include=" + searchParams.sourceInclude;
            }

            //searchUri += "&_source_exclude=files.namespaces";

            //searchUri += "&analyzer=hashAnalyzer";

            WebClient client = new WebClient();

            // BUGBUG: DownloadString will throw a WebException (The remote server returned an error: (400) Bad Request.) if the query string is invalid.
            // e.g. unbalanced quotations or field queries without values. 
            // Should we throw and let the client handle these 'exceptions'?
            string resultString = client.DownloadString(searchUri);

            JObject elasticSearchResponse = JsonConvert.DeserializeObject<JObject>(resultString);

            return resultString;
        }
    }
}

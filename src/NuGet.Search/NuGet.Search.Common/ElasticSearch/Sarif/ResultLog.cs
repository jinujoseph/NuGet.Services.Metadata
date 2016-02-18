using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType(IdProperty = "Id")]
    public sealed class ResultLog : IIndexDocument
    {
        public ResultLog()
        {
        }

        public ResultLog(string id)
        {
            this.Id = id;
        }

        [Date(Ignore = true)]
        public DateTime SavedOn { get; set; }

        [String]
        public string SyntaxKind { get { return SarifKind.ResultLog.ToString(); } }

        [String]
        public string Version { get; set; }

        public IList<RunLog> RunLogs { get; set; }

        [String]
        public string Id { get; set; }

        public string GetDocumentId()
        {
            return this.Id;
        }
    }
}

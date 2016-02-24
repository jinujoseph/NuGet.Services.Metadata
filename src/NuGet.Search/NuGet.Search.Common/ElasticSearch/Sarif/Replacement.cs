using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class Replacement
    {
        [String]
        public string SyntaxKind { get { return SarifKind.Replacement.ToString(); } }

        [Number]
        public int Offset { get; set; }

        [Number]
        public int DeletedLength { get; set; }

        [Number]
        public int InsertedBytes { get; set; }
    }
}

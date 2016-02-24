using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class Hash
    {
        [String]
        public string SyntaxKind { get { return SarifKind.Hash.ToString(); } }

        [String]
        public string Value { get; set; }

        [String]
        public string Algorithm { get; set; }
    }
}

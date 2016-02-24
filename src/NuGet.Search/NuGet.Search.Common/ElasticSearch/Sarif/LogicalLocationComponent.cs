using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class LogicalLocationComponent
    {
        [String]
        public string SyntaxKind { get { return SarifKind.LogicalLocationComponent.ToString(); } }

        [String]
        public string Name { get; set; }

        [String]
        public string Kind { get; set; }
    }
}

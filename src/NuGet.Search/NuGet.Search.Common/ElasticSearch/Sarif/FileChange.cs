using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class FileChange
    {
        [String]
        public string SyntaxKind { get { return SarifKind.FileChange.ToString(); } }

        [String]
        public string Uri { get; set; }

        public IList<Replacement> Replacements { get; set; }
    }
}

using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class Fix
    {
        [String]
        public string SyntaxKind { get { return SarifKind.Fix.ToString(); } }

        [String]
        public string Description { get; set; }

        public IList<FileChange> FileChanges { get; set; }
    }
}

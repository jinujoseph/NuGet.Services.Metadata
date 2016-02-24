using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class AnnotatedCodeLocation
    {
        [String]
        public string SyntaxKind { get { return SarifKind.AnnotatedCodeLocation.ToString(); } }

        public IList<PhysicalLocationComponent> PhysicalLocation { get; set; }

        [String]
        public string Message { get; set; }
    }
}

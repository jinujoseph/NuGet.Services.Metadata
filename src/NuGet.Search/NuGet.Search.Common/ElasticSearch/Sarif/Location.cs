using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class Location
    {
        [String]
        public string SyntaxKind { get { return SarifKind.Location.ToString(); } }

        public IList<PhysicalLocationComponent> AnalysisTarget { get; set; }

        public IList<PhysicalLocationComponent> ResultFile { get; set; }

        public IList<LogicalLocationComponent> LogicalLocation { get; set; }

        public string FullyQualifiedLogicalName { get; set; }

        public Dictionary<string, string> Properties { get; set; }
    }
}

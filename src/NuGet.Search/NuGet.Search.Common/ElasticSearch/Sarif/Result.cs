using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    public sealed class Result
    {
        [String]
        public string SyntaxKind { get { return SarifKind.Result.ToString(); } }

        [String]
        public string RuleId { get; set; }

        [String]
        public string Kind { get; set; }

        [String]
        public string FullMessage { get; set; }

        [String]
        public string ShortMessage { get; set; }

        public FormattedMessage FormattedMessage { get; set; }

        public IList<Location> Locations { get; set; }

        [String]
        public string ToolFingerprint { get; set; }

        public IList<AnnotatedCodeLocation> Stacks { get; set; }

        public IList<IList<AnnotatedCodeLocation>> ExecutionFlows { get; set; }

        public IList<AnnotatedCodeLocation> RelatedLocations { get; set; }

        [String]
        public bool IsSuppressedInSource { get; set; }

        public IList<Fix> Fixes { get; set; }

        public Dictionary<string, string> Properties { get; set; }

    }
}

using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class RunLog
    {
        [String]
        public string SyntaxKind { get { return SarifKind.RunLog.ToString(); } }

        public ToolInfo ToolInfo { get; set; }

        public RunInfo RunInfo { get; set; }

        public IList<RuleDescriptor> RuleInfo { get; set; }

        public IList<Result> Results { get; set; }
    }
}

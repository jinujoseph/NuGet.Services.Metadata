using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class RunInfo
    {
        [String]
        public string SyntaxKind { get { return SarifKind.RunInfo.ToString(); } }

        [String]
        public string InvocationInfo { get; set; }

        public IList<FileReference> AnalysisTargets { get; set; }

        [Date]
        public DateTime RunDate { get; set; }

        [String]
        public string Machine { get; set; }

        [String]
        public int ProcessId { get; set; }
    }
}

using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    public sealed class RunInfo
    {
        [String]
        public string SyntaxKind { get { return SarifKind.RunInfo.ToString(); } }

        [String]
        public string InvocationInfo { get; set; }

        public IList<FileReference> AnalysisTargets { get; set; }
    }
}

using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class ToolInfo
    {
        [String]
        public string SyntaxKind { get { return SarifKind.ToolInfo.ToString(); } }

        [String]
        public string Name { get; set; }

        [String]
        public string FullName { get; set; }

        [String]
        public string Version { get; set; }

        [String]
        public string FileVersion { get; set; }
    }
}

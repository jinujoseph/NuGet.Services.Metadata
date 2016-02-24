using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class RuleDescriptor
    {
        [String]
        public string SyntaxKind { get { return SarifKind.RuleDescriptor.ToString(); } }

        [String]
        public string Id { get; set; }

        [String]
        public string Name { get; set; }

        [String]
        public string ShortDescription { get; set; }

        [String]
        public string FullDescription { get; set; }

        public Dictionary<string, string> Options { get; set; }

        public Dictionary<string, string> FormatSpecifiers { get; set; }

        [String]
        public string HelpUri { get; set; }

        public Dictionary<string, string> Properties { get; set; }
    }
}

using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class Region
    {
        [String]
        public string SyntaxKind { get { return SarifKind.Region.ToString(); } }

        [Number]
        public int StartLine { get; set; }

        [Number]
        public int StartColumn { get; set; }

        [Number]
        public int EndLine { get; set; }

        [Number]
        public int EndColumn { get; set; }

        [Number]
        public int CharOffset { get; set; }

        [Number]
        public int ByteOffset { get; set; }

        [Number]
        public int Length { get; set; }
    }
}

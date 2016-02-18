using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    public sealed class PhysicalLocationComponent
    {
        [String]
        public string SyntaxKind { get { return SarifKind.PhysicalLocationComponent.ToString(); } }

        [String]
        public string Uri { get; set; }

        [String]
        public string MimeType { get; set; }

        public Region Region { get; set; }
    }
}

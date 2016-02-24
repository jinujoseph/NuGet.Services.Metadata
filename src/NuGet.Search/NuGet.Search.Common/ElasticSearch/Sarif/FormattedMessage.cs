using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    [ElasticsearchType]
    public sealed class FormattedMessage
    {
        [String]
        public string SyntaxKind { get { return SarifKind.FormattedMessage.ToString(); } }

        [String]
        public string SpecifierId { get; set; }

        public IList<string> Arguments { get; set; }
    }
}

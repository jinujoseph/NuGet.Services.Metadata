using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    public sealed class FileReference
    {
        [String]
        public string SyntaxKind { get { return SarifKind.FileReference.ToString(); } }

        [String]
        public string Uri { get; set; }

        public IList<Hash> Hashes { get; set; }
    }

}

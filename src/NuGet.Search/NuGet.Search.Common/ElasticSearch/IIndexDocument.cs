
using System;

namespace NuGet.Search.Common.ElasticSearch
{
    public interface IIndexDocument
    {
        DateTime SavedOn { get; set; }
        string GetDocumentId();
    }
}

using NuGet.Search.Common.ElasticSearch.Sarif;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.IndexerService
{
    public interface ISarifProvider
    {
        IEnumerable<ResultLog> GetNextBatch();
    }
}

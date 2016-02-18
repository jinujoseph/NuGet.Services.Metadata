using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common.ElasticSearch.Sarif
{
    public enum SarifKind
    {
        None,
        AnnotatedCodeLocation,
        FileChange,
        FileReference,
        Fix,
        FormattedMessage,
        Hash,
        Location,
        LogicalLocationComponent,
        PhysicalLocationComponent,
        Region,
        Replacement,
        Result,
        ResultLog,
        RuleDescriptor,
        RunInfo,
        RunLog,
        ToolInfo,
    }
}

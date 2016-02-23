using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Sdk;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Sarif.Rules
{
    [Export(typeof(IRuleDescriptor))]
    public class NoLatestStableVersion : NgRuleBase
    {
        const string ID = "NG003";
        const string SHORTDESCRIPTION = "No latest stable version of package.";

        public NoLatestStableVersion() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

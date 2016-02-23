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
    public class NoLatestPrereleaseVersion : NgRuleBase
    {
        const string ID = "NG004";
        const string SHORTDESCRIPTION = "No latest prerelease version of package.";

        public NoLatestPrereleaseVersion() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

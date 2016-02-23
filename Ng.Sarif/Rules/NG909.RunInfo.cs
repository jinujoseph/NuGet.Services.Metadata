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
    public class RunInfo : NgRuleBase
    {
        const string ID = "NG909";
        const string SHORTDESCRIPTION = "RunInfo";

        public RunInfo() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

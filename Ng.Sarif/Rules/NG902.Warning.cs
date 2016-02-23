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
    public class Warning : NgRuleBase
    {
        const string ID = "NG902";
        const string SHORTDESCRIPTION = "Warning";

        public Warning() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

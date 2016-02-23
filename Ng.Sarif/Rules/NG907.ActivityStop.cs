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
    public class ActivityStop : NgRuleBase
    {
        const string ID = "NG907";
        const string SHORTDESCRIPTION = "Activity stopped";

        public ActivityStop() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

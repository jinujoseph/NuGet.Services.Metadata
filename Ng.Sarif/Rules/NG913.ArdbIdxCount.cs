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
    public class ArdbIdxCount : NgRuleBase
    {
        const string ID = "NG913";
        const string SHORTDESCRIPTION = "ArdbIdxCount";

        public ArdbIdxCount() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

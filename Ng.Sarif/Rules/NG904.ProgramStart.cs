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
    public class ProgramStart : NgRuleBase
    {
        const string ID = "NG904";
        const string SHORTDESCRIPTION = "Program started";

        public ProgramStart() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

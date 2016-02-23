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
    public class Error : NgRuleBase
    {
        const string ID = "NG903";
        const string SHORTDESCRIPTION = "Error";

        public Error() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

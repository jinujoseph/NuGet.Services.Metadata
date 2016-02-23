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
    public class RegistrationUrlNotFound : NgRuleBase
    {
        const string ID = "NG010";
        const string SHORTDESCRIPTION = "The registration URL could not be found (404).";

        public RegistrationUrlNotFound() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

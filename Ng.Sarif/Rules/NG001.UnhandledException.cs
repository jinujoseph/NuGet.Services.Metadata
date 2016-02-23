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
    public class UnhandledException : NgRuleBase
    {
        const string ID = "NG001";
        const string SHORTDESCRIPTION = "The program threw an unhandled exception.";

        public UnhandledException() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

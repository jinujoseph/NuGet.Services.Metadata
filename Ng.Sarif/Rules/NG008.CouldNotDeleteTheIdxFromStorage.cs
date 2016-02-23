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
    public class CouldNotDeleteTheIdxFromStorage : NgRuleBase
    {
        const string ID = "NG008";
        const string SHORTDESCRIPTION = "Could not delete the Idx file from storage.";

        public CouldNotDeleteTheIdxFromStorage() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

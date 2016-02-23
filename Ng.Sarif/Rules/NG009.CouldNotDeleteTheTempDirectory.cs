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
    public class CouldNotDeleteTheTempDirectory : NgRuleBase
    {
        const string ID = "NG009";
        const string SHORTDESCRIPTION = "Could not delete the temp directory.";

        public CouldNotDeleteTheTempDirectory() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

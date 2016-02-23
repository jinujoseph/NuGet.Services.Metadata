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
    public class CouldNotDownloadPackage : NgRuleBase
    {
        const string ID = "NG005";
        const string SHORTDESCRIPTION = "Could not download package.";

        public CouldNotDownloadPackage() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

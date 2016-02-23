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
    public class CouldNotCreateIdxFile : NgRuleBase
    {
        const string ID = "NG006";
        const string SHORTDESCRIPTION = "Could not create Idx file.";

        public CouldNotCreateIdxFile() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

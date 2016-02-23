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
    public class PackageDownloadUrlNotFound : NgRuleBase
    {
        const string ID = "NG011";
        const string SHORTDESCRIPTION = "The package download URL could not be found (404).";

        public PackageDownloadUrlNotFound() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

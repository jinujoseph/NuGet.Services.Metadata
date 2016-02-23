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
    public class DownloadsPackageCount : NgRuleBase
    {
        const string ID = "NG910";
        const string SHORTDESCRIPTION = "DownloadsPackageCount";

        public DownloadsPackageCount() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}

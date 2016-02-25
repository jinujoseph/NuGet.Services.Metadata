﻿using Microsoft.CodeAnalysis.Sarif;
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
    public class ListedPackageCount : NgRuleBase
    {
        const string ID = "NG911";
        const string SHORTDESCRIPTION = "ListedPackageCount";

        public ListedPackageCount() : base(ID, SHORTDESCRIPTION)
        {
        }
    }
}
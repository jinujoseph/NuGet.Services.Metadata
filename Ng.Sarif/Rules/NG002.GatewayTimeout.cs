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
    public class GatewayTimeout : NgRuleBase
    {
        const string ID = "NG002";
        const string SHORTDESCRIPTION = "A network call failed with a 504 response, Gateway Timeout";
        const string TSGURI = "https://microsoft.sharepoint.com/teams/managedlanguages/_layouts/OneNote.aspx?id=%2Fteams%2Fmanagedlanguages%2Ffiles%2FTeam%20Notebook%2FRoslyn%20Team%20Notebook&wd=target%28Nuget%20Package%20Indexing.one%7C18A177D8-12C8-4AC2-9D80-5E5E6BB65F5D%2FNG.exe%20stops%20with%20%28504%5C%29%20Gateway%20Timeout%20error%7CD1E8C12E-1688-44D7-8282-8B906D01B7C1%2F%29";

        public GatewayTimeout() : base(ID, SHORTDESCRIPTION, new Uri(TSGURI))
        {
        }
    }
}

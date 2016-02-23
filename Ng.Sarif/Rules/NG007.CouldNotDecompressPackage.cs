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
    public class CouldNotDecompressPackage : NgRuleBase
    {
        const string ID = "NG007";
        const string SHORTDESCRIPTION = "Could not decompress package.";
        const string TSGURI = "https://microsoft.sharepoint.com/teams/managedlanguages/_layouts/OneNote.aspx?id=%2Fteams%2Fmanagedlanguages%2Ffiles%2FTeam%20Notebook%2FRoslyn%20Team%20Notebook&wd=target%28Nuget%20Package%20Indexing.one%7C18A177D8-12C8-4AC2-9D80-5E5E6BB65F5D%2FNG.exe%20failed%20to%20Decompress%20the%20package%7C28DB276A-3E79-44FB-982D-BB31CC95AE87%2F%29";

        public CouldNotDecompressPackage() : base(ID, SHORTDESCRIPTION, new Uri(TSGURI))
        {
        }
    }
}

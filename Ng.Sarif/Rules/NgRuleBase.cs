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
    public abstract class NgRuleBase : IRuleDescriptor
    {
        const string TSGURL = "https://microsoft.sharepoint.com/teams/managedlanguages/files/team notebook/roslyn team notebook?wd=target%28Nuget%20Package%20Indexing.one%7C18A177D8-12C8-4AC2-9D80-5E5E6BB65F5D%2FTroubleshooting%20Guides%7C84E746A8-C3DD-487C-A896-8A2C251F2D4C%2F%29";

        public NgRuleBase(string id, string shortDescription) : this(id, shortDescription, new Uri(TSGURL))
        {
        }

        public NgRuleBase(string id, string shortDescription, Uri helpUri) : this(id, shortDescription, shortDescription, helpUri)
        {
        }

        public NgRuleBase(string id, string shortDescription, string fullDescription, Uri helpUri)
        {
            this.Id = id;
            this.ShortDescription = shortDescription;
            this.FullDescription = fullDescription;
            this.HelpUri = helpUri;

            this.FormatSpecifiers = new Dictionary<string, string>();
            this.Options = new Dictionary<string, string>();
            this.Properties = new Dictionary<string, string>();
        }

        public string Id
        {
            get;
            protected set;
        }

        public string Name
        {
            get
            {
                return this.GetType().Name;
            }
        }

        public string ShortDescription
        {
            get;
            protected set;
        }

        public string FullDescription
        {
            get;
            protected set;
        }

        public Uri HelpUri
        {
            get;
            protected set;
        }

        public Dictionary<string, string> FormatSpecifiers
        {
            get;
            protected set;
        }

        public Dictionary<string, string> Options
        {
            get;
            protected set;
        }

        public Dictionary<string, string> Properties
        {
            get;
            protected set;
        }
    }
}

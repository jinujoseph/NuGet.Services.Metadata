using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Search.Website.Models
{
    public class SearchParams
    {
        public string index { get; set; }
        public string searchText { get; set; }
        public string docType { get; set; }
        public string sort { get; set; }
        public string page { get; set; }
        public string pageSize { get; set; }
        public string take { get; set; }
        public string skip { get; set; }
        public string sourceInclude { get; set; }
    }
}
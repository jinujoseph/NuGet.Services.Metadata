using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Search.Website.Models
{
    public class TagAndDispositionInfo
    {
        public string Index { get; set; }
        public string Query { get; set; }
        public string DocType { get; set; }
        public string[] Tags { get; set; }
        public string Disposition { get; set; }

        public static void ValidateModel(TagAndDispositionInfo tagAndDispositionInfo)
        {
            if (tagAndDispositionInfo == null)
            {
                throw new ArgumentNullException("tagAndDispositionInfo is required.");
            }

            if (String.IsNullOrWhiteSpace(tagAndDispositionInfo.Index))
            {
                throw new ArgumentNullException("tagAndDispositionInfo.Index is required");
            }

            if (String.IsNullOrWhiteSpace(tagAndDispositionInfo.Query))
            {
                throw new ArgumentNullException("tagAndDispositionInfo.Query is required");
            }

            if (String.IsNullOrWhiteSpace(tagAndDispositionInfo.DocType))
            {
                throw new ArgumentNullException("tagAndDispositionInfo.DocType is required");
            }
        }
    }
}
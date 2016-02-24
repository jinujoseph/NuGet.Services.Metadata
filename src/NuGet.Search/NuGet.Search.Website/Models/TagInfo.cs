using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Search.Website.Models
{
    public class TagInfo
    {
        public string Index { get; set; }
        public string DocumentId { get; set; }
        public string DocType { get; set; }
        public string[] Tags { get; set; }

        public static void ValidateModel(TagInfo tagInfo)
        {
            if (tagInfo == null)
            {
                throw new ArgumentNullException("tagInfo is required.");
            }

            if (String.IsNullOrWhiteSpace(tagInfo.Index))
            {
                throw new ArgumentNullException("tagInfo.Index is required");
            }

            if (String.IsNullOrWhiteSpace(tagInfo.DocumentId))
            {
                throw new ArgumentNullException("tagInfo.DocumentId is required");
            }

            if (String.IsNullOrWhiteSpace(tagInfo.DocType))
            {
                throw new ArgumentNullException("tagInfo.DocType is required");
            }
        }
    }
}
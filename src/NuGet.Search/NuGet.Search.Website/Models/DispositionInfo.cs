using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Search.Website.Models
{
    public class DispositionInfo
    {
        public string Index { get; set; }
        public string DocumentId { get; set; }
        public string DocType { get; set; }
        public string Disposition { get; set; }

        public static void ValidateModel(DispositionInfo dispositionInfo)
        {
            if (dispositionInfo == null)
            {
                throw new ArgumentNullException("dispositionInfo is required.");
            }

            if (String.IsNullOrWhiteSpace(dispositionInfo.Index))
            {
                throw new ArgumentNullException("dispositionInfo.Index is required");
            }

            if (String.IsNullOrWhiteSpace(dispositionInfo.DocumentId))
            {
                throw new ArgumentNullException("dispositionInfo.DocumentId is required");
            }

            if (String.IsNullOrWhiteSpace(dispositionInfo.DocType))
            {
                throw new ArgumentNullException("dispositionInfo.DocType is required");
            }
        }
    }
}
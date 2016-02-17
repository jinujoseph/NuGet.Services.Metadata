using System;

namespace NuGet.Search.Common
{
    public enum DocumentType
    {
        Undefined = 0,
        PackageDocument = 1,
    }

    public static class DocumentTypeParser
    {
        public static DocumentType GetDocumentType(string documentTypeName, bool tryParse = true)
        {
            DocumentType documentType = DocumentType.Undefined;

            if (!Enum.TryParse<DocumentType>(documentTypeName, true, out documentType) && !tryParse)
            {
                throw new ArgumentException(String.Format("Document type \"{0}\" could not be parsed.", documentType));
            }

            return documentType;
        }
    }
}
using NuGet.Search.Common.ElasticSearch;
using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace NuGet.Search.Common
{
    public static class XmlSerializable
    {
        public static void Serialize<T>(this T document, Stream outputStream) where T : IIndexDocument
        {
            document.SavedOn = DateTime.Now;

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            serializer.Serialize(outputStream, document);
        }

        public static void Serialize<T>(this T document, string filePath) where T : IIndexDocument
        {
            try
            {
                string directory = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    XmlSerializable.Serialize(document, stream);
                }
            }
            catch
            {
                File.Delete(filePath);
                throw;
            }
        }

        public static T Deserialize<T>(Stream inputStream) where T : IIndexDocument
        {
            T document;
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            document = (T)serializer.Deserialize(inputStream);

            return document;
        }

        public static T Deserialize<T>(string filePath, bool checkExists = false) where T : class, IIndexDocument
        {
            if (checkExists)
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }
            }

            using (FileStream stream = new FileStream(filePath, FileMode.Open))
            {
                return XmlSerializable.Deserialize<T>(stream);
            }
        }
    }
}

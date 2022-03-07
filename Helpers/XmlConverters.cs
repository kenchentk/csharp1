using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace StoreFront2.Helpers
{
    public static class XmlConverters
    {
        public static XmlDocument ToXmlDocument(this XDocument xDocument)
        {
            var xmlDocument = new XmlDocument();
            using (var reader = xDocument.CreateReader())
            {
                xmlDocument.Load(reader);
            }

            var xDeclaration = xDocument.Declaration;
            if (xDeclaration != null)
            {
                var xmlDeclaration = xmlDocument.CreateXmlDeclaration(
                    xDeclaration.Version,
                    xDeclaration.Encoding,
                    xDeclaration.Standalone);

                xmlDocument.InsertBefore(xmlDeclaration, xmlDocument.FirstChild);
            }

            return xmlDocument;
        }
    }
}
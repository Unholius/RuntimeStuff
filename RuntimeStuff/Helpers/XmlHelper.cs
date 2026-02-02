using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RuntimeStuff.Helpers
{
    public static class XmlHelper
    {
        public static string[] GetValues(string xml, string nodeName)
        {
            if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(nodeName))
            {
                return new string[0];
            }

            try
            {
                XDocument doc = XDocument.Parse(xml);
                return doc.Descendants()
                          .Where(x => x.Name.LocalName == nodeName)
                          .Select(x => x.Value)
                          .ToArray();
            }
            catch (Exception)
            {
                return new string[0];
            }
        }
    }
}

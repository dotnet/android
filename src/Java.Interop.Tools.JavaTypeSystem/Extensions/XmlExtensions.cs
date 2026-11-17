using System;
using System.Xml;
using System.Xml.Linq;

namespace Java.Interop.Tools.JavaTypeSystem
{
	static class XmlExtensions
	{
		public static string XGetAttribute (this XElement element, string name)
		{
			return XGetAttributeOrNull (element, name) ?? string.Empty;
		}

		public static string? XGetAttributeOrNull (this XElement element, string name)
		{
			return element.Attribute (name)?.Value.Trim ();
		}

		public static bool XGetAttributeAsBool (this XElement element, string name)
		{
			return XGetAttributeOrNull (element, name) == "true";
		}

		public static void WriteAttributeStringIfValue (this XmlWriter writer, string attributeName, string? value)
		{
			if (value.HasValue ())
				writer.WriteAttributeString (attributeName, value);
		}
	}
}

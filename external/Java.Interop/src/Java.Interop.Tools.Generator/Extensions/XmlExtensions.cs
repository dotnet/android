using System;
using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Xamarin.Android.Tools
{
	static class XmlExtensions
	{
		public static string? XGetAttribute (this XElement element, string name)
			=> element.Attribute (name)?.Value.Trim ();

		public static string? XGetAttribute (this XPathNavigator nav, string name, string ns)
			=> nav.GetAttribute (name, ns)?.Trim ();

		public static int? XGetAttributeAsInt (this XElement element, string name)
		{
			var value = element.XGetAttribute (name);

			if (int.TryParse (value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				return result;

			return null;
		}
	}
}

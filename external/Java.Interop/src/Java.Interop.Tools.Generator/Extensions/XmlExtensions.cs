using System;
using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;

using Java.Interop.Tools.Generator;

namespace Xamarin.Android.Tools
{
	static class XmlExtensions
	{
		public static string? XGetAttribute (this XElement element, string name)
			=> element.Attribute (name)?.Value.Trim ();

		public static string? XGetAttribute (this XPathNavigator nav, string name, string ns)
			=> nav.GetAttribute (name, ns)?.Trim ();

		public static AndroidSdkVersion? XGetAttributeAsAndroidSdkVersion (this XElement element, string name)
		{
			var value = element.XGetAttribute (name);

			if (AndroidSdkVersion.TryParse (value, out var result))
				return result;

			return null;
		}
	}
}

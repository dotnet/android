using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using Java.Interop.Tools.Generator;

namespace Xamarin.Android.Tools {

	static class XmlExtensions {

		public static string XGetAttribute (this XElement element, string name)
		{
			var attr = element.Attribute (name);
			return attr != null ? attr.Value.Trim () : null;
		}

		public static string XGetAttribute (this XPathNavigator nav, string name, string ns)
		{
			var attr = nav.GetAttribute (name, ns);
			return attr != null ? attr.Trim () : null;
		}

		public static AndroidSdkVersion? XGetAttributeAsAndroidSdkVersionOrNull (this XElement element, string name)
		{
			var attr = element.Attribute (name);

			if (attr?.Value is null)
				return null;

			if (AndroidSdkVersion.TryParse (attr.Value, out var val))
				return val;

			return null;
		}
	}
}

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;

namespace Xamarin.Android.Tools {

	static class XmlExtensions {

		public static string XGetAttribute (this XmlElement element, string name)
		{
			var attr = element.GetAttribute (name);
			return attr != null ? attr.Trim () : null;
		}

		public static string XGetAttribute (this XPathNavigator nav, string name, string ns)
		{
			var attr = nav.GetAttribute (name, ns);
			return attr != null ? attr.Trim () : null;
		}
	}
}

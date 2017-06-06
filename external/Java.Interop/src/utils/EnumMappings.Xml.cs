using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation {

	partial class EnumMappings {

		internal static bool IsXml (string file)
		{
			using (var s = File.OpenText (file)) {
				int c = s.Read ();
				return c == '<';
			}
		}

		internal static TextReader FieldXmlToCsv (string file)
		{
			if (file == null)
				return null;

			var sw = new StringWriter ();
			var doc = XDocument.Load (file, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			foreach (var e in doc.XPathSelectElements ("/enum-field-mappings/mapping")) {
				string enu = GetMandatoryAttribute (e, "clr-enum-type");
				string jni_type = e.Attribute ("jni-class") != null
					? e.XGetAttribute ("jni-class")
					: e.Attribute ("jni-interface") != null
						? "I:" + e.XGetAttribute ("jni-interface")
						: GetMandatoryAttribute (e, "jni-class or jni-interface");
				bool bitfield = e.Attribute ("bitfield") != null && e.XGetAttribute ("bitfield") == "true";
				foreach (var m in e.XPathSelectElements ("field")) {
					string verstr = m.Attribute ("api-level") != null
						? m.XGetAttribute ("api-level")
						: "0";
					string member   = GetMandatoryAttribute (m, "clr-name");
					string jni_name = GetMandatoryAttribute (m, "jni-name");
					string value    = GetMandatoryAttribute (m, "value");
					sw.WriteLine ("{0}, {1}, {2}, {3}, {4}{5}", verstr, enu, member, jni_type + '.' + jni_name, value, bitfield ? ", Flags" : null);
				}
			}

			return new StringReader (sw.ToString ());
		}

		static string GetMandatoryAttribute (XElement e, string name)
		{
			if (e.Attribute (name) == null) {
				throw new InvalidOperationException (String.Format ("Mandatory attribute '{0}' is missing on a mapping element: {1}", name, e.ToString ()));
			}
			return e.XGetAttribute (name);
		}
	}
}


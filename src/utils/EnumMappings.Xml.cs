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

			return FieldXmlToCsv (XDocument.Load (file, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo));
		}

		internal static TextReader FieldXmlToCsv (XDocument doc)
		{
			var sw = new StringWriter ();

			foreach (var e in doc.XPathSelectElements ("/enum-field-mappings/mapping")) {

				var enu = GetMandatoryAttribute (e, "clr-enum-type");
				var jni_type = e.XGetAttribute ("jni-class") ?? "I:" + e.XGetAttribute ("jni-interface");

				// If neither jni was specified leave it blank
				if (jni_type == "I:")
					jni_type = string.Empty;

				var bitfield = e.XGetAttribute ("bitfield") == "true";

				foreach (var m in e.XPathSelectElements ("field")) {
					var verstr   = m.XGetAttribute ("api-level") ?? "0";
					var member   = GetMandatoryAttribute (m, "clr-name");
					var jni_name = m.XGetAttribute ("jni-name");
					var value    = GetMandatoryAttribute (m, "value");

					var jni_member = string.IsNullOrWhiteSpace (jni_name) ? string.Empty : jni_type + '.' + jni_name;

					sw.WriteLine ("{0}, {1}, {2}, {3}, {4}{5}", verstr, enu, member, jni_member, value, bitfield ? ", Flags" : null);
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


using System;
using System.IO;
using System.Linq;
using System.Xml;

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
			var doc = new XmlDocument ();
			doc.Load (file);

			foreach (XmlElement e in doc.SelectNodes ("/enum-field-mappings/mapping")) {
				string enu = GetMandatoryAttribute (e, "clr-enum-type");
				string jni_type = e.HasAttribute ("jni-class")
					? e.XGetAttribute ("jni-class")
					: e.HasAttribute ("jni-interface")
						? "I:" + e.XGetAttribute ("jni-interface")
						: GetMandatoryAttribute (e, "jni-class or jni-interface");
				bool bitfield = e.HasAttribute ("bitfield") && e.XGetAttribute ("bitfield") == "true";
				foreach (XmlElement m in e.SelectNodes ("field")) {
					string verstr = m.HasAttribute ("api-level")
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

		static string GetMandatoryAttribute (XmlElement e, string name)
		{
			if (!e.HasAttribute (name)) {
				throw new InvalidOperationException (String.Format ("Mandatory attribute '{0}' is missing on a mapping element: {1}", name, e.OuterXml));
			}
			return e.XGetAttribute (name);
		}
	}
}


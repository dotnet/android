using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Mono.Options;

using Xamarin.Android.Tools;

namespace MonoDroid.Generation {

	partial class EnumMappings {

		public class EnumDescription {
			public Dictionary<string, string> Members = new Dictionary<string, string> ();
			public Dictionary<string, string> JniNames = new Dictionary<string, string> ();
			public bool BitField;
			public bool FieldsRemoved;
		}

		internal Dictionary<string, EnumDescription> ParseXmlFieldMappings (string csv, int filter_version, IList<KeyValuePair<string, string>> remove_nodes)
		{
			return ParseFieldMappings (FieldXmlToCsv (csv), null, filter_version, remove_nodes);
		}

		internal Dictionary<string, EnumDescription> ParseFieldMappings (string csv, string flagsFile, int filter_version, IList<KeyValuePair<string, string>> remove_nodes)
		{
			if (csv == null)
				return new Dictionary<string, EnumDescription> ();
			using (var csr = new StreamReader (csv))
				return ParseFieldMappings (csr, flagsFile != null ? File.ReadAllLines (flagsFile) : null, filter_version, remove_nodes);
		}

		// key: Enum name
		// value: Dictionary:
		//	key: enum element name
		//	value: enum element value
		internal Dictionary<string, EnumDescription> ParseFieldMappings (TextReader source, string [] enumFlags, int filter_version, IList<KeyValuePair<string, string>> remove_nodes)
		{
			var enums = new Dictionary<string, EnumDescription> ();
			if (source == null)
				return enums;
			bool transient = false;

			string s;
			string last_enum = null;
			EnumDescription enumDescription = null;

			while ((s = source.ReadLine ()) != null) {
				try {
					if (string.IsNullOrEmpty (s) || s.StartsWith ("//"))
						continue;
					if (s == "- ENTER TRANSIENT MODE -") {
						transient = true;
						continue;
					}

					string[] pieces   = s.Split (',');

					string verstr     = pieces[0].Trim ();
					string enu        = pieces[1].Trim ();
					string member     = pieces[2].Trim ();
					string java_name  = pieces[3].Trim ();
					string value      = pieces[4].Trim ();

					if (filter_version > 0 && filter_version < int.Parse (verstr))
						continue;

					if (!string.IsNullOrEmpty (java_name.Trim ()))
						remove_nodes.Add (new KeyValuePair<string, string> (java_name.Trim (), transient ? enu : null));

					// This is a line that only deletes a const, not maps it to an enum
					if (string.IsNullOrEmpty (enu))
						continue;

					// If this is a new enum, add the old one and reset things
					if (last_enum != enu) {
						if (last_enum != null)
							enums.Add (last_enum, enumDescription);

						last_enum = enu;
						enumDescription = new EnumDescription () { FieldsRemoved = !transient };
					}

					if (pieces.Length > 5)
						enumDescription.BitField = pieces [5].Trim () == "Flags";
					
					if (enumFlags != null && enumFlags.Contains (enu))
						enumDescription.BitField = true;

					// Add this member to the enum
					enumDescription.Members.Add (member, value);
					enumDescription.JniNames.Add (member, java_name);
				} catch (Exception ex) {
					Report.Error (Report.ErrorEnumMapping + 0, "ERROR at parsing enum " + s, ex);
					throw;
				}
			}

			// Make sure the last enum gets added to the list
			if (last_enum != null)
				enums.Add (last_enum, enumDescription);

			return enums;
		}

		internal void ParseJniMember (string jniMember, out string package, out string type, out string member)
		{
			try {
				DoParseJniMember (jniMember, out package, out type, out member);
			} catch (Exception ex) {
				throw new Exception (string.Format ("failed to parse enum mapping: JNI member: {0}", jniMember, ex));
			}
		}

		static void DoParseJniMember (string jniMember, out string package, out string type, out string member)
		{
			int endPackage  = jniMember.LastIndexOf ('/');
			int endClass    = jniMember.LastIndexOf ('.');

			package = jniMember.Substring (0, endPackage).Replace ('/', '.');
			if (package.StartsWith ("I:"))
				package = package.Substring (2);

			if (endClass >= 0) {
				type    = jniMember.Substring (endPackage + 1, endClass - endPackage - 1).Replace ('$', '.');
				member  = jniMember.Substring (endClass + 1);
			} else {
				type    = jniMember.Substring (endPackage + 1).Replace ('$', '.');
				member  = "";
			}
		}

		internal void WriteEnumerations (string output_dir, Dictionary<string, EnumDescription> enums, GenBase [] gens, bool useShortFileNames)
		{
			if (!Directory.Exists (output_dir))
				Directory.CreateDirectory (output_dir);

			foreach (var enu in enums) {
				using (StreamWriter sw = new StreamWriter (Path.Combine (output_dir, GetFileName (enu.Key, useShortFileNames) + ".cs"), false)) {
					string ns = enu.Key.Substring (0, enu.Key.LastIndexOf ('.')).Trim ();
					string enoom = enu.Key.Substring (enu.Key.LastIndexOf ('.') + 1).Trim ();

					sw.WriteLine ("namespace {0} {{", ns);
					if (enu.Value.BitField)
						sw.WriteLine ("  [System.Flags]");
					sw.WriteLine ("  public enum {0} {{", enoom);

					foreach (var member in enu.Value.Members) {
						var managedMember = FindManagedMember (ns, enoom, enu.Value, member.Key, gens);
						if (managedMember != null)
							sw.WriteLine ("    [global::Android.Runtime.IntDefinition (\"" + managedMember + "\")]");
						sw.WriteLine ("    {0} = {1},", member.Key.Trim (), member.Value.Trim ());
					}
					sw.WriteLine ("  }");
					sw.WriteLine ("}");
				}
			}
		}

		string FindManagedMember (string ns, string enumName, EnumDescription desc, string enumFieldName, IEnumerable<GenBase> gens)
		{
			if (desc.FieldsRemoved)
				return null;

			var jniMember = desc.JniNames [enumFieldName];
			if (string.IsNullOrWhiteSpace (jniMember)) {
				// enum values like "None" falls here.
				return null;
			}
			return FindManagedMember (jniMember, gens);
		}

		WeakReference cache_found_class;

		string FindManagedMember (string jniMember, IEnumerable<GenBase> gens)
		{
			string package, type, member;
			ParseJniMember (jniMember, out package, out type, out member);
			var fullJavaType = (string.IsNullOrEmpty (package) ? "" : package + ".") + type;

			var cls = cache_found_class != null ? cache_found_class.Target as GenBase : null;
			if (cls == null || cls.JniName != fullJavaType) {
				cls = gens.FirstOrDefault (g => g.JavaName == fullJavaType);
				if (cls == null) {
					// The class was not found e.g. removed by metadata fixup.
					return null;
				}
				cache_found_class = new WeakReference (cls);
			}
			var fld = cls.Fields.FirstOrDefault (f => f.JavaName == member);
			if (fld == null) {
				// The field was not found e.g. removed by metadata fixup.
                                return null;
			}
			return cls.FullName + "." + fld.Name;
		}

		Dictionary<string,string> file_name_map = new Dictionary<string, string> ();

		string GetFileName (string file, bool useShortFileNames)
		{
			if (!useShortFileNames)
				return file;
			string s;
			if (file_name_map.TryGetValue (file, out s))
				return s;
			s = file_name_map.Count.ToString ();
			file_name_map [file] = s;
			return s;
		}

		internal void WriteEnumerationRegistrations (StreamWriter sw, Dictionary<string, EnumDescription> enums)
		{
			foreach (var enu in enums)
				sw.WriteLine ("  <add-node path=\"/api\"><enum name=\"{0}\" /></add-node>", enu.Key);
		}

		StringReader MethodXmlToCsv (string file)
		{
			if (file == null)
				return null;

			var sw = new StringWriter ();
			var doc = new XmlDocument ();
			doc.Load (file);
			foreach (XmlElement e in doc.SelectNodes ("/enum-method-mappings/mapping")) {
				string version = e.XGetAttribute ("api-level");
				string package = null;
				string java_class = null;
				if (e.HasAttribute ("jni-class")) {
					string c    = e.XGetAttribute ("jni-class");
					int    s    = c.LastIndexOf ('/');
					package     = c.Substring (0, s).Replace ('/', '.');
					java_class  = c.Substring (s+1).Replace ('$', '.');
				} else if (e.HasAttribute ("jni-interface")) {
					string c    = e.XGetAttribute ("jni-interface");
					int    s    = c.LastIndexOf ('/');
					package     = c.Substring (0, s).Replace ('/', '.');
					java_class  = "[Interface]" + c.Substring (s+1).Replace ('$', '.');
				} else {
					throw new InvalidOperationException (string.Format ("Missing mandatory attribute 'jni-class' or 'jni-interface' on a mapping element: {0}", e.OuterXml));
				}

				foreach (XmlElement m in e.SelectNodes ("method")) {
					string method     = GetMandatoryAttribute (m, "jni-name");
					string parameter  = GetMandatoryAttribute (m, "parameter");
					string enum_type  = GetMandatoryAttribute (m, "clr-enum-type");

					sw.WriteLine ("{0}, {1}, {2}, {3}, {4}, {5}", String.IsNullOrEmpty (version) ? "0" : version, package, java_class, method, parameter, enum_type);
				}
			}
			return new StringReader (sw.ToString ());
		}

		internal List<ApiTransform> ParseXmlMethodMappings (string file, int filter_version)
		{
			return ParseMethodMappings (MethodXmlToCsv (file), filter_version);
		}

		internal List<ApiTransform> ParseMethodMappings (string file, int filter_version)
		{
			if (file == null)
				return new List<ApiTransform> ();
			using (StreamReader sr = new StreamReader (file))
				return ParseMethodMappings (sr, filter_version);
		}

		internal List<ApiTransform> ParseMethodMappings (TextReader source, int filter_version)
		{
			var list = new List<ApiTransform> ();
			if (source == null)
				return list;

			string s;
			bool preserveTypeMode = false;

			while ((s = source.ReadLine ()) != null) {
				if (s.Trim () == "---- PRESERVE TYPE MODE ----")
					preserveTypeMode = true;

				if (s.Length == 0 || s.StartsWith ("//", StringComparison.Ordinal))
					continue;
				var items = s.Split (',');
				int ver;
				if (filter_version > 0 && int.TryParse (items [0], out ver) && filter_version < ver)
					continue;
				try {
					list.Add (new ApiTransform (preserveTypeMode, items));
				} catch (Exception ex) {
					Report.Error (Report.ErrorEnumMapping + 0, "Failed to process enum mapping. Text line: " + s, ex);
					throw;
				}
			}

			return list;
		}
	}
}

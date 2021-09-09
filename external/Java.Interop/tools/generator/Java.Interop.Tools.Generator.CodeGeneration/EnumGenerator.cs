using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static MonoDroid.Generation.EnumMappings;

namespace MonoDroid.Generation
{
	class EnumGenerator
	{
		protected TextWriter sw;

		public EnumGenerator (TextWriter writer)
		{
			sw = writer;
		}

		public void WriteEnumeration (KeyValuePair<string, EnumDescription> enu, GenBase [] gens)
		{
			string ns = enu.Key.Substring (0, enu.Key.LastIndexOf ('.')).Trim ();
			string enoom = enu.Key.Substring (enu.Key.LastIndexOf ('.') + 1).Trim ();

			sw.WriteLine ("namespace {0} {{", ns);
			if (enu.Value.BitField)
				sw.WriteLine ("\t[System.Flags]");
			sw.WriteLine ("\tpublic enum {0} {{", enoom);

			foreach (var member in enu.Value.Members) {
				var managedMember = FindManagedMember (enu.Value, member.Key, gens);
				sw.WriteLine ("\t\t[global::Android.Runtime.IntDefinition (" + (managedMember != null ? "\"" + managedMember + "\"" : "null") + ", JniField = \"" + StripExtraInterfaceSpec (enu.Value.JniNames [member.Key]) + "\")]");
				sw.WriteLine ("\t\t{0} = {1},", member.Key.Trim (), member.Value.Trim ());
			}
			sw.WriteLine ("\t}");
			sw.WriteLine ("}");
		}

		string FindManagedMember (EnumDescription desc, string enumFieldName, IEnumerable<GenBase> gens)
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
			int endPackage = jniMember.LastIndexOf ('/');
			int endClass = jniMember.LastIndexOf ('.');

			package = jniMember.Substring (0, endPackage).Replace ('/', '.');
			if (package.StartsWith ("I:", StringComparison.Ordinal))
				package = package.Substring (2);

			if (endClass >= 0) {
				type = jniMember.Substring (endPackage + 1, endClass - endPackage - 1).Replace ('$', '.');
				member = jniMember.Substring (endClass + 1);
			} else {
				type = jniMember.Substring (endPackage + 1).Replace ('$', '.');
				member = "";
			}
		}

		string StripExtraInterfaceSpec (string jniFieldSpec)
		{
			return jniFieldSpec.StartsWith ("I:", StringComparison.Ordinal) ? jniFieldSpec.Substring (2) : jniFieldSpec;
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using generator.SourceWriters;
using Java.Interop.Tools.Generator.Enumification;
using Xamarin.SourceWriter;
using static MonoDroid.Generation.EnumMappings;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

namespace MonoDroid.Generation
{
	class EnumGenerator
	{
		protected TextWriter sw;

		public EnumGenerator (TextWriter writer)
		{
			sw = writer;
		}

		public void WriteEnumeration (CodeGenerationOptions opt, KeyValuePair<string, EnumDescription> enu, GenBase [] gens)
		{
			var ns = enu.Key.Substring (0, enu.Key.LastIndexOf ('.')).Trim ();
			var cw = new CodeWriter (sw);

			cw.WriteLine ($"namespace {ns}");
			cw.WriteLine ("{");

			var enoom = CreateWriter (opt, enu, gens);
			enoom.Write (cw);

			cw.WriteLine ("}");
		}

		EnumWriter CreateWriter (CodeGenerationOptions opt, KeyValuePair<string, EnumDescription> enu, GenBase [] gens)
		{
			var enoom = new EnumWriter {
				Name = enu.Key.Substring (enu.Key.LastIndexOf ('.') + 1).Trim (),
				IsPublic = true
			};

			if (enu.Value.BitField)
				enoom.Attributes.Add (new FlagsAttr ());

			foreach (var member in enu.Value.Members) {
				var m = new EnumMemberWriter {
					Name = member.EnumMember.Trim (),
					Value = member.Value.Trim (),
				};

				var managedMember = FindManagedMember (enu.Value, member, gens);

				if (opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1)
					m.Attributes.Add (new IntDefinitionAttr (managedMember, StripExtraInterfaceSpec (member.JavaSignature)));

				enoom.Members.Add (m);
			}

			return enoom;
		}

		string FindManagedMember (EnumDescription desc, ConstantEntry member, IEnumerable<GenBase> gens)
		{
			if (desc.FieldsRemoved)
				return null;

			var jniMember = member.JavaSignature;
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

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

				// Try to find the original field in our model
				var managedMember = FindManagedMember (enu.Value, member, gens);
				var managedMemberName = managedMember != null ? $"{managedMember.Value.Cls.FullName}.{managedMember.Value.Field.Name}" : null;

				if (opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1)
					m.Attributes.Add (new IntDefinitionAttr (managedMemberName, StripExtraInterfaceSpec (member.JavaSignature)));

				SourceWriterExtensions.AddSupportedOSPlatform (m.Attributes, member.ApiLevel, opt);

				if (member.DeprecatedSince.HasValue) {
					// If we've manually marked it as obsolete in map.csv, that takes precedence
					var msg = member.DeprecatedSince.Value == -1 ? "This value was incorrectly added to the enumeration and is not a valid value" : "deprecated";
					SourceWriterExtensions.AddObsolete (m.Attributes, msg, opt, deprecatedSince: member.DeprecatedSince);
				} else if (managedMember != null && managedMember.Value.Field?.DeprecatedComment?.Contains ("enum directly instead of this field") == false) {
					// Some of our source fields may have been marked with:
					// "This constant will be removed in the future version. Use XXX enum directly instead of this field."
					// We don't want this message to propogate to the enum.
					SourceWriterExtensions.AddObsolete (m.Attributes, managedMember.Value.Field.DeprecatedComment, opt, deprecatedSince: managedMember.Value.Field.DeprecatedSince);
				}

				enoom.Members.Add (m);
			}

			return enoom;
		}

		WeakReference cache_found_class;

		(GenBase Cls, Field Field)? FindManagedMember (EnumDescription desc, ConstantEntry constant, IEnumerable<GenBase> gens)
		{
			if (desc.FieldsRemoved)
				return null;

			var jniMember = constant.JavaSignature;

			if (string.IsNullOrWhiteSpace (jniMember)) {
				// enum values like "None" falls here.
				return null;
			}

			ParseJniMember (jniMember, out var package, out var type, out var member);

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
			return (cls, fld);
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

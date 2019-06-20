using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using Java.Interop.Tools.TypeNameMappings;

using Xamarin.Android.Binder;

using MonoDroid.Utils;

namespace MonoDroid.Generation
{

	public abstract class ClassGen : GenBase {

		bool needs_new;
		bool inherits_object;
		List<Ctor> ctors = new List<Ctor> ();
		List<string> explicitly_implemented_iface_methods = new List<string> (); // do not initialize here; see FixupMethodOverides()
		bool fill_explicit_implementation_started;

		protected ClassGen (GenBaseSupport support)
			: base (support)
		{
			inherits_object = true;
			if (Namespace == "Java.Lang" && (Name == "Object" || Name == "Throwable"))
				inherits_object = false;
		}

		public override string DefaultValue {
			get { return "IntPtr.Zero"; }
		}

		internal    bool    InheritsObject {
			get { return inherits_object; }
		}
		
		public List<string> ExplicitlyImplementedInterfaceMethods {
			get { return explicitly_implemented_iface_methods; }
		}

		public abstract bool IsAbstract { get; }

		public abstract bool IsFinal { get; }

		public override string NativeType {
			get { return "IntPtr"; }
		}

		public IList<Ctor> Ctors {
			get { return ctors; }
		}

		public abstract string BaseType { get; set; }

		public bool NeedsNew {
			get => needs_new;
			set => needs_new = value;
		}

		public bool ContainsCtor (string jni_sig)
		{
			foreach (Ctor c in ctors)
				if (c.JniSignature == jni_sig)
					return true;
			return false;
		}

		public bool ContainsNestedType (GenBase gen)
		{
			if (BaseGen != null && BaseGen.ContainsNestedType (gen))
				return true;

			return HasNestedType (gen.Name);
		}

		public override string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null) 
		{
			return String.Format ("JNIEnv.ToLocalJniHandle ({0})", varname);
		}

		public override string FromNative (CodeGenerationOptions opt, string varname, bool owned) 
		{
			return String.Format ("global::Java.Lang.Object.GetObject<{0}> ({1}, {2})", opt.GetOutputName (FullName), varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
		}

		public override void ResetValidation ()
		{
			validated = false;
			base.ResetValidation ();
		}

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			if (validated)
				return is_valid;
			
			validated = true;

			if (!support.OnValidate (opt)) {
				is_valid = false;
				return false;
			}

			// We're validating this in prior to BaseType.
			if (TypeParameters != null && !TypeParameters.Validate (opt, type_params)) {
				is_valid = false;
				return false;
			}

			if (Char.IsNumber (Name [0])) {
				// it is an anonymous class which does not need output.
				is_valid = false;
				return false;
			}
			
			base_symbol = IsAnnotation ? opt.SymbolTable.Lookup ("java.lang.Object") : BaseType != null ? opt.SymbolTable.Lookup (BaseType) : null;
			if (base_symbol == null && FullName != "Java.Lang.Object" && FullName != "System.Object") {
				Report.Warning (0, Report.WarningClassGen + 2, "Class {0} has unknown base type {1}.", FullName, BaseType);
				is_valid = false;
				return false;
			}

			if ((base_symbol != null && !base_symbol.Validate (opt, TypeParameters)) || !base.OnValidate (opt, type_params)) {
				Report.Warning (0, Report.WarningClassGen + 3, "Class {0} has invalid base type {1}.", FullName, BaseType);
				is_valid = false;
				return false;
			}

			List<Ctor> valid_ctors = new List<Ctor> ();
			foreach (Ctor c in ctors)
				if (c.Validate (opt, TypeParameters))
					valid_ctors.Add (c);
			ctors = valid_ctors;

			return true;
		}

		public override void FixupAccessModifiers (CodeGenerationOptions opt)
		{
			while (!IsAnnotation && !string.IsNullOrEmpty (BaseType)) {
				var baseClass = opt.SymbolTable.Lookup (BaseType) as ClassGen;
				if (baseClass != null && RawVisibility == "public" && baseClass.RawVisibility != "public") {
					//Skip the BaseType and copy over any "missing" methods
					foreach (var baseMethod in baseClass.Methods) {
						var method = Methods.FirstOrDefault (m => m.Matches (baseMethod));
						if (method == null)
							Methods.Add (baseMethod);
					}
					BaseType = baseClass.BaseType;
				} else {
					break;
				}
			}

			base.FixupAccessModifiers (opt);
		}
		
		public override void FixupExplicitImplementation ()
		{
			if (fill_explicit_implementation_started)
				return; // already done.
			fill_explicit_implementation_started = true;
			if (BaseGen != null && BaseGen.explicitly_implemented_iface_methods == null)
				BaseGen.FixupExplicitImplementation ();

			foreach (InterfaceGen iface in GetAllDerivedInterfaces ()) {
				if (iface.IsGeneric) {
					bool skip = false;
					foreach (ISymbol isym in Interfaces) {
						var gs = isym as GenericSymbol;
						if (gs != null && gs.IsConcrete && gs.Gen == iface)
							skip = true;
					}
					if (skip)
						continue; // we don't handle it here; generic interface methods are generated in different manner.
				}
				if (BaseGen != null && BaseGen.GetAllDerivedInterfaces ().Contains (iface))
					continue; // no need to fill members for already-implemented-in-base-class iface.
				foreach (var m in iface.Methods.Where (m => !ContainsMethod (m, false, false))) {
					string sig = m.GetSignature ();
					bool doExplicitly = false;
					if (IsCovariantMethod (m))
						doExplicitly = true;
					else if (m.IsGeneric)
						doExplicitly = true;
					if (doExplicitly)
						explicitly_implemented_iface_methods.Add (sig);
				}
			}
			
			// Keep in sync with Generate() that generates explicit iface method impl.
			foreach (ISymbol isym in Interfaces) {
				if (isym is GenericSymbol) {
					GenericSymbol gs = isym as GenericSymbol;
					if (gs.IsConcrete) {
						foreach (Method m in gs.Gen.Methods)
							if (m.IsGeneric) {
								explicitly_implemented_iface_methods.Add (m.GetSignature ());
							}
					}
				}
			}

			foreach (var nt in NestedTypes)
				nt.FixupExplicitImplementation ();
		}

		public override ClassGen BaseGen {
			get { return (base_symbol is GenericSymbol ? (base_symbol as GenericSymbol).Gen : base_symbol) as ClassGen; }
		}

		internal IEnumerable<InterfaceExtensionInfo> GetNestedInterfaceTypes ()
		{
			var nestedInterfaces = new List<InterfaceExtensionInfo> ();
			AddNestedInterfaceTypes (this, nestedInterfaces);
			return nestedInterfaces;
		}

		static void AddNestedInterfaceTypes (GenBase type, List<InterfaceExtensionInfo> nestedInterfaces)
		{
			foreach (GenBase nt in type.NestedTypes) {
				InterfaceGen ni = nt as InterfaceGen;
				if (ni != null)
					nestedInterfaces.Add (new InterfaceExtensionInfo {
							DeclaringType = type.FullName.Substring (type.Namespace.Length+1).Replace (".","_"),
							Type          = ni
					});
				else
					AddNestedInterfaceTypes (nt, nestedInterfaces);
			}
		}
		
		public bool IsExplicitlyImplementedMethod (string sig)
		{
			for (ClassGen c = this; c != null; c = c.BaseGen)
				if (c.explicitly_implemented_iface_methods.Contains (sig))
					return true;
			return false;
		}

		public override void Generate (CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			gen_info.CurrentType = FullName;

			StreamWriter sw = gen_info.Writer = gen_info.OpenStream(opt.GetFileName (FullName));

			sw.WriteLine ("using System;");
			sw.WriteLine ("using System.Collections.Generic;");
			sw.WriteLine ("using Android.Runtime;");
			if (opt.CodeGenerationTarget != CodeGenerationTarget.XamarinAndroid) {
				sw.WriteLine ("using Java.Interop;");
			}
			sw.WriteLine ();
			sw.WriteLine ("namespace {0} {{", Namespace);
			sw.WriteLine ();

			var generator = opt.CreateCodeGenerator (sw);
			generator.WriteClass (this, "\t", gen_info);

			sw.WriteLine ("}");
			sw.Close ();
			gen_info.Writer = null;
		}

		public static void GenerateTypeRegistrations (CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			StreamWriter sw = gen_info.Writer = gen_info.OpenStream (opt.GetFileName ("Java.Interop.__TypeRegistrations"));

			Dictionary<string, List<KeyValuePair<string, string>>> mapping = new Dictionary<string, List<KeyValuePair<string, string>>>();
			foreach (KeyValuePair<string, string> reg in gen_info.TypeRegistrations) {
				int ls          = reg.Key.LastIndexOf ('/');
				string package  = ls >= 0 ? reg.Key.Substring (0, ls) : "";

				if (JavaNativeTypeManager.ToCliType (reg.Key) == reg.Value)
					continue;
				List<KeyValuePair<string, string>> v;
				if (!mapping.TryGetValue (package, out v))
					mapping.Add (package, v = new List<KeyValuePair<string, string>>());
				v.Add (new KeyValuePair<string, string>(reg.Key, reg.Value));
			}

			sw.WriteLine ("using System;");
			sw.WriteLine ("using System.Collections.Generic;");
			sw.WriteLine ("using Android.Runtime;");
			sw.WriteLine ();
			sw.WriteLine ("namespace Java.Interop {");
			sw.WriteLine ();
			sw.WriteLine ("\tpartial class __TypeRegistrations {");
			sw.WriteLine ();
			sw.WriteLine ("\t\tpublic static void RegisterPackages ()");
			sw.WriteLine ("\t\t{");
			sw.WriteLine ("#if MONODROID_TIMING");
			sw.WriteLine ("\t\t\tvar start = DateTime.Now;");
			sw.WriteLine ("\t\t\tAndroid.Util.Log.Info (\"MonoDroid-Timing\", \"RegisterPackages start: \" + (start - new DateTime (1970, 1, 1)).TotalMilliseconds);");
			sw.WriteLine ("#endif // def MONODROID_TIMING");
			sw.WriteLine ("\t\t\tJava.Interop.TypeManager.RegisterPackages (");
			sw.WriteLine ("\t\t\t\t\tnew string[]{");
			foreach (KeyValuePair<string, List<KeyValuePair<string, string>>> e in mapping) {
				sw.WriteLine ("\t\t\t\t\t\t\"{0}\",", e.Key);
			}
			sw.WriteLine ("\t\t\t\t\t},");
			sw.WriteLine ("\t\t\t\t\tnew Converter<string, Type>[]{");
			foreach (KeyValuePair<string, List<KeyValuePair<string, string>>> e in mapping) {
				sw.WriteLine ("\t\t\t\t\t\tlookup_{0}_package,", e.Key.Replace ('/', '_'));
			}
			sw.WriteLine ("\t\t\t\t\t});");
			sw.WriteLine ("#if MONODROID_TIMING");
			sw.WriteLine ("\t\t\tvar end = DateTime.Now;");
			sw.WriteLine ("\t\t\tAndroid.Util.Log.Info (\"MonoDroid-Timing\", \"RegisterPackages time: \" + (end - new DateTime (1970, 1, 1)).TotalMilliseconds + \" [elapsed: \" + (end - start).TotalMilliseconds + \" ms]\");");
			sw.WriteLine ("#endif // def MONODROID_TIMING");
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();
			sw.WriteLine ("\t\tstatic Type Lookup (string[] mappings, string javaType)");
			sw.WriteLine ("\t\t{");
			sw.WriteLine ("\t\t\tstring managedType = Java.Interop.TypeManager.LookupTypeMapping (mappings, javaType);");
			sw.WriteLine ("\t\t\tif (managedType == null)");
			sw.WriteLine ("\t\t\t\treturn null;");
			sw.WriteLine ("\t\t\treturn Type.GetType (managedType);");
			sw.WriteLine ("\t\t}");
			foreach (KeyValuePair<string, List<KeyValuePair<string, string>>> map in mapping) {
				sw.WriteLine ();
				string package = map.Key.Replace ('/', '_');
				sw.WriteLine ("\t\tstatic string[] package_{0}_mappings;", package);
				sw.WriteLine ("\t\tstatic Type lookup_{0}_package (string klass)", package);
				sw.WriteLine ("\t\t{");
				sw.WriteLine ("\t\t\tif (package_{0}_mappings == null) {{", package);
				sw.WriteLine ("\t\t\t\tpackage_{0}_mappings = new string[]{{", package);
				map.Value.Sort ((a, b) => a.Key.CompareTo (b.Key));
				foreach (KeyValuePair<string, string> t in map.Value) {
					sw.WriteLine ("\t\t\t\t\t\"{0}:{1}\",", t.Key, t.Value);
				}
				sw.WriteLine ("\t\t\t\t};");
				sw.WriteLine ("\t\t\t}");
				sw.WriteLine ("");
				sw.WriteLine ("\t\t\treturn Lookup (package_{0}_mappings, klass);", package);
				sw.WriteLine ("\t\t}");
			}
			sw.WriteLine ("\t}");
			sw.WriteLine ("}");
			sw.Close ();
			gen_info.Writer = null;
		}

		public static void GenerateEnumList (GenerationInfo gen_info)
		{
			StreamWriter sw = new StreamWriter (File.Create (Path.Combine (gen_info.CSharpDir, "enumlist")));
			foreach (string e in gen_info.Enums)
				sw.WriteLine (e);
			sw.Close ();
		}
		
		protected override bool GetEnumMappedMemberInfo ()
		{
			foreach (var m in Ctors)
				return true;
			return base.GetEnumMappedMemberInfo ();
		}
		
		public override void UpdateEnumsInInterfaceImplementation ()
		{
			foreach (InterfaceGen iface in GetAllDerivedInterfaces ()) {
				if (iface.HasEnumMappedMembers) {
					foreach (Method imethod in iface.Methods) {
						var method = Methods.FirstOrDefault (m => m.Name == imethod.Name && m.JniSignature == imethod.JniSignature);
						if (method != null) {
							if (imethod.IsReturnEnumified)
								method.RetVal.SetGeneratedEnumType (imethod.RetVal.FullName);
							for (int i = 0; i < imethod.Parameters.Count; i++)
								if (imethod.Parameters [i].IsEnumified)
									method.Parameters [i].SetGeneratedEnumType (imethod.Parameters [i].Type);
						}
					}
				}
			}
			base.UpdateEnumsInInterfaceImplementation ();
		}
	}
}


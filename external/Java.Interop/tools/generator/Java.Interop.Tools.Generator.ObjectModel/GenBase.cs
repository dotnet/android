using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonoDroid.Generation.Utilities;

namespace MonoDroid.Generation
{
	public abstract class GenBase : IGeneratable, ApiVersionsSupport.IApiAvailability
	{
		bool enum_updated;
		bool property_filled;
		bool property_filling;
		protected internal ISymbol base_symbol;
		protected bool iface_validation_failed;
		protected GenBaseSupport support;
		protected bool validated = false;

		readonly List<string> implemented_interfaces = new List<string> ();
		readonly Dictionary<string, Method> jni_sig_hash = new Dictionary<string, Method> ();
		readonly Dictionary<string, Property> prop_hash = new Dictionary<string, Property> ();

		protected GenBase (GenBaseSupport support)
		{
			this.support = support;
		}

		public List<Field> Fields { get; private set; } = new List<Field> ();
		public List<ISymbol> Interfaces { get; } = new List<ISymbol> ();
		public List<Method> Methods { get; private set; } = new List<Method> ();
		public List<GenBase> NestedTypes { get; private set; } = new List<GenBase> ();
		public List<Property> Properties { get; } = new List<Property> ();
		public string DefaultValue { get; set; }
		public bool HasVirtualMethods { get; set; }

		// This means Ctors/Methods/Properties/Fields has not been populated yet.
		// If this type is retrieved from the SymbolTable, it will call PopulateAction
		// to fill in members before returning it to the user.
		internal bool IsShallow { get; set; }
		internal Action PopulateAction { get; set; }

		public void AddField (Field f)
		{
			Fields.Add (f);
		}

		public void AddImplementedInterface (string name)
		{
			implemented_interfaces.Add (name);
		}

		public void AddMethod (Method m)
		{
			Methods.Add (m);
		}

		public virtual void AddNestedType (GenBase gen)
		{
			foreach (var nest in NestedTypes) {
				if (gen.JavaName.StartsWith (nest.JavaName + ".")) {
					nest.AddNestedType (gen);
					return;
				}
			}

			var removes = new List<GenBase> ();

			foreach (var nest in NestedTypes) {
				if (nest.JavaName.StartsWith (gen.JavaName + ".")) {
					gen.AddNestedType (nest);
					removes.Add (nest);
				}
			}

			foreach (var rmv in removes)
				NestedTypes.Remove (rmv);

			NestedTypes.Add (gen);
		}

		void AdjustNestedTypeFullName (GenBase parent)
		{
			if (parent is ClassGen)
				foreach (var nested in parent.NestedTypes)
					nested.FullName = parent.FullName + "." + nested.Name;
		}

		void AddPropertyAccessors ()
		{
			// First pass extracts getters and creates property hash
			List<Method> unmatched = new List<Method> ();
			foreach (Method m in Methods) {
				if (m.IsPropertyAccessor) {
					string prop_name = m.PropertyName;
					if (m.CanSet || prop_name == string.Empty || Name == prop_name || m.Name == "GetHashCode" || HasNestedType (prop_name) || IsInfrastructural (prop_name))
						unmatched.Add (m);
					else if (BaseGen != null && !BaseGen.prop_hash.ContainsKey (prop_name) && BaseGen.Methods.Any (mm => mm.Name == m.Name && ReturnTypeMatches (m, mm) && ParameterList.Equals (mm.Parameters, m.Parameters)))
						// this is to filter out those method that was *not* a property
						// in the base type for some reason (e.g. name overlap).
						// For example, android.graphics.drawable.BitmapDrawable#getConstantState()
						// ContainsProperty() check is required here to not exclude such methods
						// that are known to be property. AbstractSelectionKey.IsValid is an example.
						unmatched.Add (m);
					else {
						if (prop_hash.ContainsKey (prop_name)) {
							if (m.Name.StartsWith ("Get"))
								unmatched.Add (m);
							else {
								unmatched.Add (prop_hash [prop_name].Getter);
								prop_hash [prop_name].Getter = m;
							}
						} else {
							var prop = new Property (prop_name) {
								Getter = m
							};
							prop_hash [prop_name] = prop;
						}
					}
				} else
					unmatched.Add (m);
			}
			Methods = unmatched;

			// Second pass adds setters
			unmatched = new List<Method> ();
			foreach (Method m in Methods) {
				if (!m.CanSet) {
					unmatched.Add (m);
					continue;
				}

				if (Ancestors ().All (a => !a.prop_hash.ContainsKey (m.PropertyName)) && Ancestors ().Any (a => a.Methods.Any (mm => mm.Name == m.Name && ReturnTypeMatches (m, mm) && ParameterList.Equals (mm.Parameters, m.Parameters))))
					unmatched.Add (m); // base setter exists, and it was not a property.
				else if (prop_hash.ContainsKey (m.PropertyName)) {
					Property baseProp = BaseGen?.Properties.FirstOrDefault (p => p.Name == m.PropertyName);
					var prop = prop_hash [m.PropertyName];
					if (prop.Getter.RetVal.FullName == m.Parameters [0].Type &&
							prop.Getter.IsAbstract == m.IsAbstract && // SearchIterator abstract getIndex() and non-abstract setIndex()
							prop.Getter.Visibility == m.Visibility &&
							(baseProp == null || baseProp.Setter != null) &&
							prop.Getter.SourceApiLevel <= m.SourceApiLevel)
						prop.Setter = m;
					else
						unmatched.Add (m);
				} else if (prop_hash.ContainsKey ("Is" + m.PropertyName)) {
					var prop = prop_hash ["Is" + m.PropertyName];
					if (prop.Getter.RetVal.FullName == m.Parameters [0].Type &&
							prop.Getter.Visibility == m.Visibility &&
							CanMethodBeIsStyleSetter (m) &&
							prop.Getter.SourceApiLevel <= m.SourceApiLevel) {
						prop.Name = m.PropertyName;
						prop.Setter = m;
						prop_hash [m.PropertyName] = prop;
						prop_hash.Remove ("Is" + m.PropertyName);
					} else
						unmatched.Add (m);
				} else
					unmatched.Add (m);
				if (m.GenerateDispatchingSetter && prop_hash.ContainsKey (m.PropertyName))
					prop_hash [m.PropertyName].GenerateDispatchingSetter = true;
			}
			Methods = unmatched;
		}

		IEnumerable<GenBase> Ancestors ()
		{
			for (var g = BaseGen; g != null; g = g.BaseGen)
				yield return g;
		}

		// not: not currently assembly qualified, but it uses needed
		// Type.GetType() conventions such as '/' for nested types.
		public string AssemblyQualifiedName =>
			$"{Namespace}.{FullName.Substring (Namespace.Length + 1).Replace ('.', '/')}";

		public int ApiAvailableSince { get; set; }

		public virtual ClassGen BaseGen => null;

		public GenBase BaseSymbol =>
			(base_symbol is GenericSymbol ? (base_symbol as GenericSymbol).Gen : base_symbol) as GenBase;

		public string Call (CodeGenerationOptions opt, string var_name) => opt.GetSafeIdentifier (var_name);

		bool CanMethodBeIsStyleSetter (Method m)
		{
			// returns false if there is an interface which has such a property that
			// the method "will" result in the same name but lacks the corresponding setter.
			// In that case, the method should not be converted to the setter, because
			// it breaks the class due to missing interface implementation member (getter).
			// bug #5020 repro uncovered this issue.
			return GetAllDerivedInterfaces ().All (iface => !iface.Properties.Any (p => p.Name == "Is" + m.PropertyName && p.Setter == null));
		}

		public bool ContainsMethod (string name_and_jnisig) => jni_sig_hash.ContainsKey (name_and_jnisig);

		public bool ContainsMethod (Method method, bool check_ifaces) => ContainsMethod (method, check_ifaces, true);

		public bool ContainsMethod (Method method, bool check_ifaces, bool check_base_ifaces)
		{
			// background: bug #10123.
			// Visibility check was introduced - and required so far - to block "public overrides protected" methods
			// (which is allowed in Java but not in C#).
			// The problem is, it does not *always* result in error and generates callable code, and we depend on
			// that fact in several classes that implements some interface that requires "public Object clone()".
			//
			// This visibility inconsistency results in 1) error for abstract methods and 2) warning for virtual methods.
			// Hence, for abstract methods we dare to ignore visibility difference and treat it as override,
			// *then* C# compiler will report this inconsistency as error that users need to fix manually, but
			// with obvious message saying "it is because of visibility consistency",
			// not "abstract member not implemented" (it is *implemented* in different visibility and brings confusion).
			// For virtual methods, just check the visibility difference and treat as different method.
			// Regardless of whether it is actually an override or not, we just invoke Java method.
			if (jni_sig_hash.ContainsKey (method.JavaName + method.JniSignature)) {
				var bm = jni_sig_hash [method.JavaName + method.JniSignature];
				if (bm.Visibility == method.Visibility || bm.IsAbstract)
					return true;
			}
			if (check_ifaces) {
				foreach (ISymbol isym in Interfaces) {
					if ((isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) is InterfaceGen igen && igen.ContainsMethod (method, true))
						return true;
				}
			}
			return BaseSymbol != null && BaseSymbol.ContainsMethod (method, check_base_ifaces, check_base_ifaces);
		}

		public bool ContainsName (string name)
		{
			if (HasNestedType (name) || ContainsProperty (name, true))
				return true;

			return Methods.Any (m => m.Name == name);
		}

		public bool ContainsProperty (string name, bool check_ifaces) => ContainsProperty (name, check_ifaces, true);

		public bool ContainsProperty (string name, bool check_ifaces, bool check_base_ifaces) =>
			GetPropertyByName (name, check_ifaces, check_base_ifaces) != null;

		public string DeprecatedComment => support.DeprecatedComment;

		IEnumerable<GenBase> Descendants (IList<GenBase> gens)
		{
			foreach (var directDescendants in gens.Where (x => x.BaseGen == this)) {
				yield return directDescendants;
				foreach (var indirectDescendants in directDescendants.Descendants (gens)) {
					yield return indirectDescendants;
				}
			}
		}

		public string ElementType { get; set; }

		public void FillProperties ()
		{
			if (property_filled)
				return;
			if (property_filling)
				throw new Exception (); // check bad recursion
			property_filling = true;
			property_filled = true;

			if (BaseGen != null)
				BaseGen.FillProperties ();
			foreach (var iface in GetAllDerivedInterfaces ())
				iface.FillProperties ();

			AddPropertyAccessors ();

			var names = prop_hash.Keys;
			foreach (string name in names)
				Properties.Add (prop_hash [name]);
			Properties.Sort ((p1, p2) => string.CompareOrdinal (p1.Name, p2.Name));
			property_filling = false;

			foreach (var nt in NestedTypes)
				nt.FillProperties ();
		}

		public virtual void FixupAccessModifiers (CodeGenerationOptions opt)
		{
			foreach (var nt in NestedTypes)
				nt.FixupAccessModifiers (opt);
		}

		public virtual void FixupExplicitImplementation ()
		{
		}

		public void FixupMethodOverrides (CodeGenerationOptions opt)
		{
			foreach (var m in Methods.Where (m => !m.IsInterfaceDefaultMethod)) {
				for (var bt = GetBaseGen (opt); bt != null; bt = bt.GetBaseGen (opt)) {
					var bm = bt.Methods.FirstOrDefault (mm => mm.Name == m.Name && mm.Visibility == m.Visibility && ParameterList.Equals (mm.Parameters, m.Parameters));
					if (bm != null && bm.RetVal.FullName == m.RetVal.FullName) { // if return type is different, it could be still "new", not "override".
						m.IsOverride = true;
						break;
					}
				}
			}

			// Interface default methods can be overriden. We want to process them differently.
			foreach (var m in Methods.Where (m => m.IsInterfaceDefaultMethod)) {
				foreach (var bt in GetAllDerivedInterfaces ()) {
					var bm = bt.Methods.FirstOrDefault (mm => mm.Name == m.Name && ParameterList.Equals (mm.Parameters, m.Parameters));
					if (bm != null) {
						m.IsInterfaceDefaultMethodOverride = true;
						break;
					}
				}
			}

			foreach (var m in Methods) {
				if (m.Name == Name || ContainsProperty (m.Name, true) || HasNestedType (m.Name))
					m.Name = "Invoke" + m.Name;
				if ((m.Name == "ToString" && m.Parameters.Count == 0) || (BaseGen != null && BaseGen.ContainsMethod (m, true)))
					m.IsOverride = true;
			}

			foreach (var nt in NestedTypes)
				nt.FixupMethodOverrides (opt);
		}

		public abstract string FromNative (CodeGenerationOptions opt, string varname, bool owned);

		public string FullName {
			get => support.FullName;
			set => support.FullName = value;
		}

		public abstract void Generate (CodeGenerationOptions opt, GenerationInfo gen_info);

		protected void GenerateAnnotationAttribute (CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			if (ShouldGenerateAnnotationAttribute) {
				var baseName = Namespace.Length > 0 ? FullName.Substring (Namespace.Length + 1) : FullName;
				var attrClassNameBase = baseName.Substring (TypeNamePrefix.Length) + "Attribute";
				var localFullName = Namespace + (Namespace.Length > 0 ? "." : string.Empty) + attrClassNameBase;

				using (var sw = gen_info.OpenStream (opt.GetFileName (localFullName))) {
					sw.WriteLine ("using System;");
					sw.WriteLine ();
					sw.WriteLine ("namespace {0} {{", Namespace);
					sw.WriteLine ();
					sw.WriteLine ("\t[global::Android.Runtime.Annotation (\"{0}\")]", JavaName);
					sw.WriteLine ("\t{0} partial class {1} : Attribute", Visibility, attrClassNameBase);
					sw.WriteLine ("\t{");

					// An Annotation attribute property is generated for each applicable annotation method,
					// where *applicable* means java annotation compatible types. See IsTypeCommensurate().
					foreach (var method in Methods.Where (m => m.Parameters.Count == 0 &&
									      IsTypeCommensurate (opt, opt.SymbolTable.Lookup (m.RetVal.JavaName)))) {
						sw.WriteLine ("\t\t[global::Android.Runtime.Register (\"{0}\"{1})]", method.JavaName, method.AdditionalAttributeString ());
						sw.WriteLine ("\t\tpublic {0} {1} {{ get; set; }}", opt.GetOutputName (method.RetVal.FullName), method.Name);
						sw.WriteLine ();
					}
					sw.WriteLine ("\t}");
					sw.WriteLine ("}");
				}
			}
		}

		public List<InterfaceGen> GetAllDerivedInterfaces ()
		{
			var result = new List<InterfaceGen> ();
			GetAllDerivedInterfaces (result);
			return result;
		}

		void GetAllDerivedInterfaces (List<InterfaceGen> ifaces)
		{
			foreach (var isym in Interfaces) {
				if (!((isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) is InterfaceGen iface))
					continue;

				var found = false;

				foreach (var known in ifaces)
					if (known.FullName == iface.FullName)
						found = true;

				if (found)
					continue;

				ifaces.Add (iface);
				iface.GetAllDerivedInterfaces (ifaces);
			}
		}

		protected internal IEnumerable<InterfaceGen> GetAllImplementedInterfaces ()
		{
			var set = new HashSet<InterfaceGen> ();

			void visit (ISymbol isym)
			{
				if ((isym is GenericSymbol gsym ? gsym.Gen : isym) is InterfaceGen igen)
					set.Add (igen);

				if (!(isym is GenBase b))
					return;

				foreach (var i in b.Interfaces)
					visit (i);
			}

			foreach (var i in Interfaces)
				visit (i);

			return set;
		}

		public IEnumerable<Method> GetAllMethods () =>
			Methods.Concat (Properties.Select (p => p.Getter)).Concat (Properties.Select (p => p.Setter).Where (m => m != null));

		GenBase GetBaseGen (CodeGenerationOptions opt)
		{
			if (this is InterfaceGen)
				return null;

			if (BaseGen != null)
				return BaseGen;

			if (BaseSymbol == null)
				return null;

			if (opt.SymbolTable.Lookup (BaseSymbol.FullName) is GenBase bg && bg != this)
				return bg;

			return null;
		}

		protected virtual bool GetEnumMappedMemberInfo ()
		{
			foreach (var f in Fields)
				if (f.IsEnumified)
					return true;
			foreach (var m in Methods)
				if (m.IsReturnEnumified | m.Parameters.Any (p => p.IsEnumified))
					return true;
			return false;
		}

		public bool GetGenericMappings (InterfaceGen gen, Dictionary<string, string> mappings)
		{
			foreach (var sym in Interfaces) {
				if (sym is GenericSymbol gsym) {
					if (gsym.Gen.FullName == gen.FullName) {
						for (int i = 0; i < gsym.TypeParams.Length; i++)
							mappings [gsym.Gen.TypeParameters [i].Name] = gsym.TypeParams [i].FullName;
						return true;
					} else if (gsym.Gen.GetGenericMappings (gen, mappings)) {
						string [] keys = new string [mappings.Keys.Count];
						mappings.Keys.CopyTo (keys, 0);
						foreach (string tp in keys)
							mappings [tp] = gsym.TypeParams [Array.IndexOf ((from gtp in gsym.Gen.TypeParameters select gtp.Name).ToArray (), mappings [tp])].FullName;
						return true;
					}
				}
			}

			return false;
		}

		public virtual string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public string GetObjectHandleProperty (string variable)
		{
			var handleType = IsThrowable () ? "Java.Lang.Throwable" : "Java.Lang.Object";

			return $"((global::{handleType}) {variable}).Handle";
		}

		public Property GetPropertyByName (string name, bool check_ifaces) =>
			GetPropertyByName (name, check_ifaces, true);

		public Property GetPropertyByName (string name, bool check_ifaces, bool check_base_ifaces)
		{
			if (prop_hash.ContainsKey (name))
				return prop_hash [name];

			if (check_ifaces) {
				foreach (ISymbol isym in Interfaces) {
					if (!((isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) is InterfaceGen igen))
						continue;

					var ret = igen.GetPropertyByName (name, true);
					if (ret != null)
						return ret;
				}
			}

			return BaseSymbol?.GetPropertyByName (name, check_base_ifaces, check_base_ifaces);
		}

		public bool HasEnumMappedMembers => GetEnumMappedMemberInfo ();

		protected internal bool HasNestedType (string name) => NestedTypes.Any (g => g.Name == name);

		public IEnumerable<GenBase> Invalidate ()
		{
			IsValid = false;
			validated = true;

			foreach (var nt in NestedTypes) {
				foreach (var sub in nt.Invalidate ())
					yield return sub;
				yield return nt;
			}
		}

		public bool IsAcw => support.IsAcw;

		public bool IsAnnotation =>
			implemented_interfaces.Any (n => n == "Java.Lang.Annotation.Annotation" || n == "java.lang.annotation.Annotation");

		public bool IsArray => false;

		// TODO: check that method.ReturnType is a superclass of m.ReturnType
		public bool IsCovariantMethod (Method method) =>
			Methods.Any (m => m.Name == method.Name && ParameterList.Equals (m.Parameters, method.Parameters));

		public bool IsDeprecated => support.IsDeprecated;

		public bool IsEnum => false;

		public bool IsGeneratable => support.IsGeneratable;

		public bool IsGeneric => support.IsGeneric;

		// some names are reserved for use by us, e.g. we don't want another
		// Handle property, as that conflicts with Java.Lang.Object.Handle.
		bool IsInfrastructural (string name) => ObjectRequiresNew.Contains (name);

		public bool IsObfuscated => support.IsObfuscated;

		bool IsThrowable () =>
			FullName == "Java.Lang.Throwable" || Ancestors ().Any (a => a.FullName == "Java.Lang.Throwable");

		// This is not a perfect match with Java language specification http://docs.oracle.com/javase/specs/jls/se5.0/html/interfaces.html#9.7
		// as it does not cover java.lang.Enum. Though C# attributes cannot handle JLE.
		// Class literal (FooBar.class) cannot be supported either.
		// We might be able to support System.Type for JLC and custom generated .NET Enums for JLE in the future.
		static bool IsTypeCommensurate (CodeGenerationOptions opt, ISymbol sym)
		{
			if (sym == null)
				return false;
			if (sym is StringSymbol)
				return true;
			if (sym is SimpleSymbol) {
				switch (sym.JavaName) {
					case "boolean":
					case "char":
					case "byte":
					case "short":
					case "int":
					case "long":
					case "float":
					case "double":
						return true;
				}
			}
			if (sym is ArraySymbol arr)
				return IsTypeCommensurate (opt, opt.SymbolTable.Lookup (arr.ElementType));
			if (sym is GenericSymbol)
				return sym.JavaName == "java.lang.Class";

			// outside Mono.Android.dll they are ManagedClassGen.
			if (sym is ClassGen)
				return sym.JavaName == "java.lang.Class" || sym.JavaName == "java.lang.String";

			return false;
		}

		public IEnumerable<string> ImplementedInterfaces => implemented_interfaces;

		public bool IsValid { get; set; } = true;

		public string JavaName => $"{PackageName}.{JavaSimpleName}";

		public string JavaSimpleName => support.JavaSimpleName;

		public string JniName => $"L{RawJniName};";

		public string MetadataXPathReference {
			get {
				string type = null;

				if (this is ClassGen)
					type = "class";
				if (this is InterfaceGen)
					type = "interface";
				if (type == null)
					throw new InvalidOperationException ("Uh...xpath? this is " + GetType ().FullName);

				return $"/api/package[@name='{PackageName}']/{type}[@name='{JavaSimpleName}']";
			}
		}

		public bool MethodValidationFailed { get; set; }

		public string Name {
			get => support.Name;
			set => support.Name = value;
		}

		public string Namespace => support.Namespace;

		public string NativeType { get; set; }

		public bool NeedsPrep => true;

		static readonly HashSet<string> ObjectRequiresNew = new HashSet<string> (
			typeof (object)
				.GetMethods ()
				.Where (m => !m.Attributes.HasFlag (MethodAttributes.SpecialName) &&
						!m.Attributes.HasFlag (MethodAttributes.RTSpecialName))
				.Select (m => m.Name)
				.Concat (new [] { "Handle" }),
			StringComparer.OrdinalIgnoreCase);

		protected virtual bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			if (Name.Length > TypeNamePrefix.Length &&
			    (Name [TypeNamePrefix.Length] == '.' || char.IsDigit (Name [TypeNamePrefix.Length]))) // see bug #5111
				return false;

			if (!support.OnValidate (opt))
				return false;

			List<GenBase> valid_nests = new List<GenBase> ();
			foreach (GenBase gen in NestedTypes) {
				if (gen.Validate (opt, TypeParameters, context))
					valid_nests.Add (gen);
			}
			NestedTypes = valid_nests;

			AdjustNestedTypeFullName (this);

			foreach (string iface_name in implemented_interfaces) {
				ISymbol isym = opt.SymbolTable.Lookup (iface_name);
				if (isym != null && isym.Validate (opt, TypeParameters, context))
					Interfaces.Add (isym);
				else {
					if (isym == null)
						Report.Warning (0, Report.WarningGenBase + 0, "For type {0}, base interface {1} does not exist.", FullName, iface_name);
					else
						Report.Warning (0, Report.WarningGenBase + 0, "For type {0}, base interface {1} is invalid.", FullName, iface_name);
					iface_validation_failed = true;
				}
			}

			List<Field> valid_fields = new List<Field> ();
			foreach (Field f in Fields) {
				if (!f.Validate (opt, TypeParameters, context))
					continue;
				valid_fields.Add (f);
			}
			Fields = valid_fields;

			int method_cnt = Methods.Count;
			Methods = Methods.Where (m => ValidateMethod (opt, m, context)).ToList ();
			MethodValidationFailed = method_cnt != Methods.Count;
			foreach (Method m in Methods) {
				if (m.IsVirtual)
					HasVirtualMethods = true;
				if (m.Name == "HashCode" && m.Parameters.Count == 0) {
					m.IsOverride = true;
					m.Name = "GetHashCode";
				}
				jni_sig_hash [m.JavaName + m.JniSignature] = m;
				if ((m.Name == "ToString" && m.Parameters.Count == 0) || (BaseSymbol != null && BaseSymbol.ContainsMethod (m, true)))
					m.IsOverride = true;
			}
			return true;
		}

		public string PackageName {
			get => support.PackageName;
			set => support.PackageName = value;
		}

		public string [] PreCall (CodeGenerationOptions opt, string var_name) => new string [] { };

		public string [] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			var rgm = this as IRequireGenericMarshal;

			return new string []{
				string.Format ("{0} {1} = {5}global::Java.Lang.Object.GetObject<{4}> ({2}, {3});",
					       opt.GetOutputName (FullName),
					       opt.GetSafeIdentifier (var_name),
					       opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)),
					       owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer",
					       opt.GetOutputName (rgm != null ? (rgm.GetGenericJavaObjectTypeOverride () ?? FullName) : FullName),
					       rgm != null ? "(" + opt.GetOutputName (FullName) + ")" : string.Empty)
			};
		}

		public string [] PostCall (CodeGenerationOptions opt, string var_name) => new string [] { };

		public string [] PostCallback (CodeGenerationOptions opt, string var_name) => new string [] { };

		public string RawJniName => PackageName.Replace ('.', '/') + "/" + JavaSimpleName.Replace ('.', '$');

		public string RawVisibility => support.Visibility;

		public bool RequiresNew (string memberName)
		{
			if (ObjectRequiresNew.Contains (memberName))
				return true;

			return IsThrowable () && ThrowableRequiresNew.Contains (memberName);
		}

		public virtual void ResetValidation ()
		{
			Interfaces.Clear ();
			iface_validation_failed = false;

			foreach (var nt in NestedTypes)
				nt.ResetValidation ();
		}

		bool ReturnTypeMatches (Method m, Method mm)
		{
			if (mm.RetVal.FullName == m.RetVal.FullName)
				return true;
			if (BaseSymbol.IsGeneric && mm.RetVal.IsGeneric)
				return true; // sloppy but pass
			return false;
		}

		public bool ShouldGenerateAnnotationAttribute => IsAnnotation;

		public void StripNonBindables ()
		{
			// As of now, if we generate bindings for interface default methods, that means users will
			// have to "implement" those methods because they are declared and you have to implement
			// any declared methods in C#. That is going to be problematic a lot.
			Methods = Methods.Where (m => !m.IsInterfaceDefaultMethod).ToList ();
			NestedTypes = NestedTypes.Where (n => !n.IsObfuscated && n.Visibility != "private").ToList ();
			foreach (var n in NestedTypes)
				n.StripNonBindables ();
		}

		static readonly HashSet<string> ThrowableRequiresNew = new HashSet<string> (
			typeof (System.Exception)
				.GetMethods ()
				.Where (m => !m.Attributes.HasFlag (MethodAttributes.SpecialName) &&
						!m.Attributes.HasFlag (MethodAttributes.RTSpecialName))
				.Select (m => m.Name)
				.Concat (typeof (System.Exception).GetProperties ().Select (p => p.Name))
				.Concat (new [] { "Handle" }),
			StringComparer.OrdinalIgnoreCase);

		public abstract string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null);

		public string TypeNamePrefix => support.TypeNamePrefix;

		public GenericParameterDefinitionList TypeParameters => support.TypeParameters;

		public virtual void UpdateEnums (CodeGenerationOptions opt, AncestorDescendantCache cache)
		{
			if (enum_updated || !IsGeneratable)
				return;

			enum_updated = true;

			var baseGen = GetBaseGen (opt);

			if (baseGen != null)
				baseGen.UpdateEnums (opt, cache);

			foreach (var m in Methods) {
				m.AutoDetectEnumifiedOverrideParameters (cache);
				m.AutoDetectEnumifiedOverrideReturn (cache);
			}

			foreach (var p in Properties)
				p.AutoDetectEnumifiedOverrideProperties (cache);

			UpdateEnumsInInterfaceImplementation ();

			foreach (var ngen in NestedTypes)
				ngen.UpdateEnums (opt, cache);
		}

		public virtual void UpdateEnumsInInterfaceImplementation ()
		{
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			context.ContextTypes.Push (this);

			try {
				return IsValid = OnValidate (opt, type_params, context);
			} finally {
				context.ContextTypes.Pop ();
			}
		}

		bool ValidateMethod (CodeGenerationOptions opt, Method m, CodeGeneratorContext context) =>
			m.Validate (opt, TypeParameters, context);

		public string Visibility => string.IsNullOrEmpty (support.Visibility) ? "public" : support.Visibility;
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using MonoDroid.Utils;
using System.Runtime.InteropServices;
using System.Collections;
using System.Security.Policy;
using System.Text;

namespace MonoDroid.Generation {
	
	public abstract class GenBase : IGeneratable, ApiVersionsSupport.IApiAvailability {

		protected GenBase (GenBaseSupport support)
		{
			this.support = support;
		}
		
		protected GenBaseSupport support;

		Dictionary<string, Method> jni_sig_hash = new Dictionary<string, Method> ();
		Dictionary<string, Property> prop_hash = new Dictionary<string, Property> ();
		List<Field> fields = new List<Field> ();
		List<Method> methods = new List<Method> ();
		List<Property> props = new List<Property> ();

		protected bool iface_validation_failed;
		protected bool is_valid = true;
		protected bool validated = false;
		bool has_virtual_methods;
		bool method_validation_failed;
		bool property_filled;
		bool? has_enum_mapped_members;
		
		public abstract string DefaultValue { get; }
		
		public bool HasEnumMappedMembers {
			get {
				if (has_enum_mapped_members == null)
					has_enum_mapped_members = GetEnumMappedMemberInfo ();
				return (bool) has_enum_mapped_members;
			}
		}

		public bool IsAcw {
			get { return support.IsAcw; }
		}
		
		public bool IsDeprecated {
			get { return support.IsDeprecated; }
		}
		
		public bool IsObfuscated {
			get { return support.IsObfuscated; }
		}

		public string DeprecatedComment {
			get { return support.DeprecatedComment; }
		}

		public bool IsGeneratable {
			get { return support.IsGeneratable; }
		}
		
		public virtual ClassGen BaseGen {
			get { return null; }
		}
		
		public bool ShouldGenerateAnnotationAttribute {
			get { return IsAnnotation; }
		}

		protected bool HasVirtualMethods {
			get { return has_virtual_methods; }
		}

		public bool IsGeneric {
			get { return support.IsGeneric; }
		}

		public string FullName {
			get { return support.FullName; }
			set { support.FullName = value; }
		}

		public int ApiAvailableSince { get; set; }

		public bool IsEnum {
			get { return false; }
		}

		public bool IsArray {
			get { return false; }
		}

		public string ElementType {
			get { return null; }
		}
		
		public string PackageName {
			get { return support.PackageName; }
			set { support.PackageName = value; }
		}

		public string JavaSimpleName {
			get { return support.JavaSimpleName; }
		}

		public string JavaName {
			get { return String.Format ("{0}.{1}", PackageName, JavaSimpleName); }
		}

		public string TypeNamePrefix {
			get { return support.TypeNamePrefix; }
		}

		// not: not currently assembly qualified, but it uses needed
		// Type.GetType() conventions such as '/' for nested types.
		public string AssemblyQualifiedName {
			get { return Namespace + "." + FullName.Substring (Namespace.Length + 1).Replace ('.', '/'); }
		}

		public string RawJniName {
			get { return PackageName.Replace ('.', '/') + "/" + JavaSimpleName.Replace ('.', '$'); }
		}

		public string JniName {
			get { return "L" + RawJniName + ";"; }
		}
		
		/*
		public string Marshaler {
			get { return support.Marshaler; }
		}
		*/

		protected bool MethodValidationFailed {
			get { return method_validation_failed; }
		}

		public string Name {
			get { return support.Name; }
			set { support.Name = value; }
		}

		public string Namespace {
			get { return support.Namespace; }
		}

		public abstract string NativeType { get; }

		public List<Field> Fields {
			get { return fields; }
		}

		List<ISymbol> ifaces = new List<ISymbol> ();
		public List<ISymbol> Interfaces {
			get { return ifaces; }
		}
		
		public bool IsAnnotation {
			get { return iface_names.Any (n => n == "Java.Lang.Annotation.Annotation" || n == "java.lang.annotation.Annotation"); }
		}

		public string MetadataXPathReference {
			get {
				string type = null;
				if (this is ClassGen)
					type = "class";
				if (this is InterfaceGen)
					type = "interface";
				if (type == null)
					throw new InvalidOperationException ("Uh...xpath? this is " + this.GetType ().FullName);
				return string.Format ("/api/package[@name='{0}']/{1}[@name='{2}']",
						PackageName, type, JavaSimpleName);
			}
		}

		public string GetObjectHandleProperty (string variable)
		{
			var handleType  = "Java.Lang.Object";
			if (IsThrowable ())
				handleType  = "Java.Lang.Throwable";

			return $"((global::{handleType}) {variable}).Handle";
		}

		bool IsThrowable ()
		{
			if (FullName == "Java.Lang.Throwable" || Ancestors ().Any (a => a.FullName == "Java.Lang.Throwable"))
				return true;
			return false;
		}

		public bool RequiresNew (string memberName)
		{
			if (Array.BinarySearch (ObjectRequiresNew, memberName, StringComparer.OrdinalIgnoreCase) >= 0) {
				return true;
			}
			if (IsThrowable () && Array.BinarySearch (ThrowableRequiresNew, memberName, StringComparer.OrdinalIgnoreCase) >= 0) {
				return true;
			}
			return false;
		}

		protected IEnumerable<InterfaceGen> GetAllImplementedInterfaces ()
		{
			var set = new HashSet<InterfaceGen> ();
			Action<ISymbol> visit = null;
			visit = isym => {
				var gsym = isym as GenericSymbol;
				var igen = (gsym != null ? gsym.Gen : isym) as InterfaceGen;
				if (igen != null)
					set.Add (igen);
				GenBase b = isym as GenBase;
				if (b == null)
					return;
				foreach (var i in b.Interfaces) {
					visit (i);
				}
			};
			foreach (var i in Interfaces)
				visit (i);
			return set;
		}

		public List<Method> Methods {
			get { return methods; }
		}

		public List<Property> Properties {
			get { return props; }
		}

		public GenericParameterDefinitionList TypeParameters {
			get { return support.TypeParameters; }
		}
		
		public string RawVisibility {
			get { return support.Visibility; }
		}

		public string Visibility {
			get { return String.IsNullOrEmpty (support.Visibility) ? "public" : support.Visibility; }
		}

		protected void AddField (Field f)
		{
			fields.Add (f);
		}

		List<string> iface_names = new List<string> ();
		protected void AddInterface (string name)
		{
			iface_names.Add (name);
		}

		List<GenBase> nested_types = new List<GenBase> ();
		public List<GenBase> NestedTypes {
			get { return nested_types; }
		}

		protected bool HasNestedType (string name)
		{
			foreach (GenBase g in NestedTypes)
				if (g.Name == name)
					return true;
			return false;
		}

		protected void AddMethod (Method m)
		{
			methods.Add (m);
		}

		public virtual void AddNestedType (GenBase gen)
		{
			foreach (GenBase nest in NestedTypes) {
				if (gen.JavaName.StartsWith (nest.JavaName + ".")) {
					nest.AddNestedType (gen);
					return;
				}
			}

			List<GenBase> removes = new List<GenBase> ();
			foreach (GenBase nest in NestedTypes) {
				if (nest.JavaName.StartsWith (gen.JavaName + ".")) {
					gen.AddNestedType (nest);
					removes.Add (nest);
				}
			}

			foreach (GenBase rmv in removes)
				NestedTypes.Remove (rmv);

			NestedTypes.Add (gen);
		}

		public virtual string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public abstract string FromNative (CodeGenerationOptions opt, string varname, bool owned);
		public abstract string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null);

		public abstract void Generate (StreamWriter sw, string indent, CodeGenerationOptions opt, GenerationInfo gen_info);
		public abstract void Generate (CodeGenerationOptions opt, GenerationInfo gen_info);

		public IEnumerable<GenBase> Invalidate ()
		{
			is_valid = false;
			validated = true;

			foreach (var nt in NestedTypes) {
				foreach (var sub in nt.Invalidate ())
					yield return sub;
				yield return nt;
			}
		}

		public bool ContainsMethod (string name_and_jnisig)
		{
			return jni_sig_hash.ContainsKey (name_and_jnisig);
		}

		public bool ContainsMethod (Method method, bool check_ifaces)
		{
			return ContainsMethod (method, check_ifaces, true);
		}

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
					InterfaceGen igen = (isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) as InterfaceGen;
					if (igen != null && igen.ContainsMethod (method, true))
						return true;
				}
			}
			return BaseSymbol != null && BaseSymbol.ContainsMethod (method, check_base_ifaces, check_base_ifaces);
		}

		public bool IsCovariantMethod (Method method)
		{
			return Methods.Any (m => m.Name == method.Name && ParameterList.Equals (m.Parameters, method.Parameters));
			// TODO: check that method.ReturnType is a superclass of m.ReturnType
		}

		public bool ContainsProperty (string name, bool check_ifaces)
		{
			return ContainsProperty (name, check_ifaces, true);
		}

		public Property GetPropertyByName (string name, bool check_ifaces)
		{
			return GetPropertyByName (name, check_ifaces, true);
		}

		public bool ContainsProperty (string name, bool check_ifaces, bool check_base_ifaces)
		{
			return GetPropertyByName (name, check_ifaces, check_base_ifaces) != null;
		}

		public Property GetPropertyByName (string name, bool check_ifaces, bool check_base_ifaces)
		{
			if (prop_hash.ContainsKey (name))
				return prop_hash [name];
			if (check_ifaces) {
				foreach (ISymbol isym in Interfaces) {
					InterfaceGen igen = (isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) as InterfaceGen;
					if (igen == null)
						continue;
					var ret = igen.GetPropertyByName (name, true);
					if (ret != null)
						return ret;
				}
			}
			return BaseSymbol != null ? BaseSymbol.GetPropertyByName (name, check_base_ifaces, check_base_ifaces) : null;
		}

		public bool ContainsName (string name)
		{
			if (HasNestedType (name) || ContainsProperty (name, true))
				return true;
			foreach (Method m in methods)
				if (m.Name == name)
					return true;
			return false;
		}

		bool ValidateMethod (CodeGenerationOptions opt, Method m)
		{
			if (!m.Validate (opt, TypeParameters)) {
				return false;
			}
			return true;
		}

		void AddPropertyAccessors ()
		{
			// First pass extracts getters and creates property hash
			List<Method> unmatched = new List<Method> ();
			foreach (Method m in methods) {
				if (m.IsPropertyAccessor) {
					string prop_name = m.PropertyName;
					if (m.CanSet || prop_name == String.Empty || Name == prop_name || m.Name == "GetHashCode" || HasNestedType (prop_name) || IsInfrastructural (prop_name))
						unmatched.Add (m);
					else if (BaseGen != null && !BaseGen.prop_hash.ContainsKey (prop_name) && BaseGen.Methods.Any (mm => mm.Name == m.Name && ReturnTypeMatches (m, BaseGen, mm) && ParameterList.Equals (mm.Parameters, m.Parameters)))
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
							Property prop = new Property (prop_name);
							prop.Getter = m;
							prop_hash [prop_name] = prop;
						}
					}
				} else
					unmatched.Add (m);
			}
			methods = unmatched;

			// Second pass adds setters
			unmatched = new List<Method> ();
			foreach (Method m in methods) {
				if (!m.CanSet) {
					unmatched.Add (m);
					continue;
				}
				
				if (Ancestors ().All (a => !a.prop_hash.ContainsKey (m.PropertyName)) && Ancestors ().Any (a => a.Methods.Any (mm => mm.Name == m.Name && ReturnTypeMatches (m, a, mm) && ParameterList.Equals (mm.Parameters, m.Parameters))))
					unmatched.Add (m); // base setter exists, and it was not a property.
				else if (prop_hash.ContainsKey (m.PropertyName)) {
					Property baseProp = BaseGen != null ? BaseGen.Properties.FirstOrDefault (p => p.Name == m.PropertyName) : null;
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
			methods = unmatched;
		}
		
		IEnumerable<GenBase> Ancestors ()
		{
			for (var g = BaseGen; g != null; g = g.BaseGen)
				yield return g;
		}

		IEnumerable<GenBase> Descendants (IList<GenBase> gens)
		{
			foreach (var directDescendants in gens.Where (x => x.BaseGen == this)) {
				yield return directDescendants;
				foreach (var indirectDescendants in  directDescendants.Descendants (gens)) {
					yield return indirectDescendants;
				}
			}
		}

		bool ReturnTypeMatches (Method m, GenBase gen, Method mm)
		{
			if (mm.RetVal.FullName == m.RetVal.FullName)
				return true;
			if (BaseSymbol.IsGeneric && mm.RetVal.IsGeneric)
				return true; // sloppy but pass
			return false;
		}

		bool CanMethodBeIsStyleSetter (Method m)
		{
			// returns false if there is an interface which has such a property that
			// the method "will" result in the same name but lacks the corresponding setter.
			// In that case, the method should not be converted to the setter, because
			// it breaks the class due to missing interface implementation member (getter).
			// bug #5020 repro uncovered this issue.
			return GetAllDerivedInterfaces ().All (iface => !iface.Properties.Any (p => p.Name == "Is" + m.PropertyName && p.Setter == null));
		}

		// Keep sorted alphabetically
		static readonly string[]    ObjectRequiresNew       = new string[]{
			"Handle",
		};

		static readonly string[]    ThrowableRequiresNew    = new string []{
			"Message",
		};

		bool IsInfrastructural (string name)
		{
			// some names are reserved for use by us, e.g. we don't want another
			// Handle property, as that conflicts with Java.Lang.Object.Handle.
			return Array.BinarySearch (ObjectRequiresNew, name) >= 0;
		}

		protected void GenCharSequenceEnumerator (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()", indent);
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\treturn GetEnumerator ();", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}public System.Collections.Generic.IEnumerator<char> GetEnumerator ()", indent);
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\tfor (int i = 0; i < Length(); i++)", indent);
			sw.WriteLine ("{0}\t\tyield return CharAt (i);", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public bool GenFields (StreamWriter sw, string indent, CodeGenerationOptions opt, HashSet<string> seen = null)
		{
			bool needsProperty = false;
			foreach (Field f in fields) {
				if (ContainsName (f.Name)) {
					Report.Warning (0, Report.WarningFieldNameCollision, "Skipping {0}.{1}, due to a duplicate field, method or nested type name. {2} (Java type: {3})", this.FullName, f.Name, HasNestedType (f.Name) ? "(Nested type)" : ContainsProperty (f.Name, false) ? "(Property)" : "(Method)", this.JavaName);
					continue;
				}
				if (seen != null && seen.Contains (f.Name)) {
					Report.Warning (0, Report.WarningDuplicateField, "Skipping {0}.{1}, due to a duplicate field. (Field) (Java type: {2})", this.FullName, f.Name, this.JavaName);
					continue;
				}
				if (f.Validate (opt, TypeParameters)) {
					if (seen != null)
						seen.Add (f.Name);
					needsProperty = needsProperty || f.NeedsProperty;
					sw.WriteLine ();
					opt.CodeGenerator.WriteField (f, sw, indent, opt, this);
				}
			}
			return needsProperty;
		}

		protected ISymbol base_symbol;
		public GenBase BaseSymbol {
			get { return (base_symbol is GenericSymbol ? (base_symbol as GenericSymbol).Gen : base_symbol) as GenBase; }
		}

		public List<InterfaceGen> GetAllDerivedInterfaces ()
		{
			List<InterfaceGen> result = new List<InterfaceGen> ();
			GetAllDerivedInterfaces (result);
			return result;
		}

		void GetAllDerivedInterfaces (List<InterfaceGen> ifaces)
		{
			foreach (ISymbol isym in Interfaces) {
				InterfaceGen iface = (isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) as InterfaceGen;
				if (iface == null)
					continue;
				bool found = false;
				foreach (InterfaceGen known in ifaces)
					if (known.FullName == iface.FullName)
						found = true;
				if (found)
					continue;
				ifaces.Add (iface);
				iface.GetAllDerivedInterfaces (ifaces);
			}
		}

		public bool GetGenericMappings (InterfaceGen gen, Dictionary<string, string> mappings)
		{
			foreach (ISymbol sym in Interfaces) {
				if (sym is GenericSymbol) {
					GenericSymbol gsym = (GenericSymbol) sym;
					if (gsym.Gen.FullName == gen.FullName) {
						for (int i = 0; i < gsym.TypeParams.Length; i++)
							mappings [gsym.Gen.TypeParameters [i].Name] = gsym.TypeParams [i].FullName;
						return true;
					} else if (gsym.Gen.GetGenericMappings (gen, mappings)) {
						string[] keys = new string [mappings.Keys.Count];
						mappings.Keys.CopyTo (keys, 0);
						foreach (string tp in keys)
							mappings [tp] = gsym.TypeParams [Array.IndexOf ((from gtp in gsym.Gen.TypeParameters select gtp.Name).ToArray (), mappings [tp])].FullName;
						return true;
					}
				}
			}
			return false;
		}
		
		public virtual void ResetValidation ()
		{
			ifaces.Clear ();
			iface_validation_failed = false;
			foreach (var nt in NestedTypes)
				nt.ResetValidation ();
		}
		
		void AdjustNestedTypeFullName (GenBase parent)
		{
			if (parent is ClassGen)
				foreach (var nested in parent.NestedTypes)
					nested.FullName = parent.FullName + "." + nested.Name;
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			opt.ContextTypes.Push (this);
			try {
				return is_valid = OnValidate (opt, type_params);
			} finally {
				opt.ContextTypes.Pop ();
			}
		}

		protected virtual bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			if (Name.Length > TypeNamePrefix.Length &&
			    (Name [TypeNamePrefix.Length] == '.' || Char.IsDigit (Name [TypeNamePrefix.Length]))) // see bug #5111
				return false;
				
			if (!support.OnValidate (opt))
				return false;

			List<GenBase> valid_nests = new List<GenBase> ();
			foreach (GenBase gen in nested_types) {
				if (gen.Validate (opt, TypeParameters))
					valid_nests.Add (gen);
			}
			nested_types = valid_nests;
			
			AdjustNestedTypeFullName (this);

			foreach (string iface_name in iface_names) {
				ISymbol isym = opt.SymbolTable.Lookup (iface_name);
				if (isym != null && isym.Validate (opt, TypeParameters))
					ifaces.Add (isym);
				else {
					if (isym == null)
						Report.Warning (0, Report.WarningGenBase + 0, "For type {0}, base interface {1} does not exist.", FullName, iface_name);
					else
						Report.Warning (0, Report.WarningGenBase + 0, "For type {0}, base interface {1} is invalid.", FullName, iface_name);
					iface_validation_failed = true;
				}
			}

			List<Field> valid_fields = new List<Field> ();
			foreach (Field f in fields) {
				if (!f.Validate (opt, TypeParameters))
					continue;
				valid_fields.Add (f);
			}
			fields = valid_fields;

			int method_cnt = methods.Count;
			methods = methods.Where (m => ValidateMethod (opt, m)).ToList ();
			method_validation_failed = method_cnt != methods.Count;
			foreach (Method m in methods) {
				if (m.IsVirtual)
					has_virtual_methods = true;
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
		
		bool property_filling;

		public void StripNonBindables ()
		{
			// As of now, if we generate bindings for interface default methods, that means users will
			// have to "implement" those methods because they are declared and you have to implement
			// any declared methods in C#. That is going to be problematic a lot.
			methods = methods.Where (m => !m.IsInterfaceDefaultMethod).ToList ();
			nested_types = nested_types.Where (n => !n.IsObfuscated && n.Visibility != "private").ToList ();
			foreach (var n in nested_types)
				n.StripNonBindables ();
		}

		public virtual void FixupAccessModifiers (CodeGenerationOptions opt)
		{
			foreach (var nt in NestedTypes)
				nt.FixupAccessModifiers (opt);
		}

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
				props.Add (prop_hash [name]);
			props.Sort ((p1, p2) => string.CompareOrdinal (p1.Name, p2.Name));
			property_filling = false;
			
			foreach (var nt in NestedTypes)
				nt.FillProperties ();
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
		
		public void FixupMethodOverrides (CodeGenerationOptions opt)
		{
			foreach (Method m in methods.Where (m => !m.IsInterfaceDefaultMethod)) {
				for (var bt = this.GetBaseGen (opt); bt != null; bt = bt.GetBaseGen (opt)) {
					var bm = bt.Methods.FirstOrDefault (mm => mm.Name == m.Name && mm.Visibility == m.Visibility && ParameterList.Equals (mm.Parameters, m.Parameters));
					if (bm != null && bm.RetVal.FullName == m.RetVal.FullName) { // if return type is different, it could be still "new", not "override".
						m.IsOverride = true;
						break;
					}
				}
			}

			// Interface default methods can be overriden. We want to process them differently.
			foreach (Method m in methods.Where (m => m.IsInterfaceDefaultMethod)) {
				foreach (var bt in this.GetAllDerivedInterfaces ()) {
					var bm = bt.Methods.FirstOrDefault (mm => mm.Name == m.Name && ParameterList.Equals (mm.Parameters, m.Parameters));
					if (bm != null) {
						m.IsInterfaceDefaultMethodOverride = true;
						break;
					}
				}
			}

			foreach (Method m in methods) {
				if (m.Name == Name || ContainsProperty (m.Name, true) || HasNestedType (m.Name))
					m.Name = "Invoke" + m.Name;
				if ((m.Name == "ToString" && m.Parameters.Count == 0) || (BaseGen != null && BaseGen.ContainsMethod (m, true)))
					m.IsOverride = true;
			}

			foreach (var nt in NestedTypes)
				nt.FixupMethodOverrides (opt);
		}

		public virtual void FixupExplicitImplementation ()
		{
		}

		GenBase GetBaseGen (CodeGenerationOptions opt)
		{
			if (this is InterfaceGen)
				return null;
			if (this.BaseGen != null)
				return this.BaseGen;
			if (this.BaseSymbol == null)
				return null;
			var bg = opt.SymbolTable.Lookup (this.BaseSymbol.FullName) as GenBase;
			if (bg != null && bg != this)
				return bg;
			return null;
		}

		bool enum_updated;

		public virtual void UpdateEnums (CodeGenerationOptions opt)
		{
			if (enum_updated || !IsGeneratable)
				return;
			enum_updated = true;
			var baseGen = GetBaseGen (opt);
			if (baseGen != null)
				baseGen.UpdateEnums (opt);

			foreach (Method m in methods) {
				AutoDetectEnumifiedOverrideParameters (m, opt);
				AutoDetectEnumifiedOverrideReturn (m, opt);
			}
			foreach (Property p in Properties)
				AutoDetectEnumifiedOverrideProperties (p, opt);
			UpdateEnumsInInterfaceImplementation ();
			foreach (var ngen in NestedTypes)
				ngen.UpdateEnums (opt);
		}

		public virtual void UpdateEnumsInInterfaceImplementation ()
		{
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			var rgm = this as IRequireGenericMarshal;

			return new string[]{
				string.Format ("{0} {1} = {5}global::Java.Lang.Object.GetObject<{4}> ({2}, {3});",
				               opt.GetOutputName (FullName),
				               opt.GetSafeIdentifier (var_name),
				               opt.GetSafeIdentifier (SymbolTable.GetNativeName (var_name)),
				               owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer",
				               opt.GetOutputName (rgm != null ? (rgm.GetGenericJavaObjectTypeOverride () ?? FullName) : FullName),
				               rgm != null ? "(" + opt.GetOutputName (FullName) + ")" : string.Empty)
			};
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			return new string[]{
			};
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			return new string[]{
			};
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			return opt.GetSafeIdentifier (var_name);
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			return new string[]{
			};
		}

		public bool NeedsPrep { get { return true; } }

		protected void GenerateAnnotationAttribute (CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			if (ShouldGenerateAnnotationAttribute) {
				var baseName = Namespace.Length > 0 ? FullName.Substring (Namespace.Length + 1) : FullName;
				var attrClassNameBase = baseName.Substring (TypeNamePrefix.Length) + "Attribute";
				var localFullName = Namespace + (Namespace.Length > 0 ? "." : string.Empty) + attrClassNameBase;
				gen_info.CurrentType = localFullName;
				StreamWriter sw = gen_info.Writer = gen_info.OpenStream (opt.GetFileName (localFullName));
				sw.WriteLine ("using System;");
				sw.WriteLine ();
				sw.WriteLine ("namespace {0} {{", Namespace);
				sw.WriteLine ();
				sw.WriteLine ("\t[global::Android.Runtime.Annotation (\"{0}\")]", JavaName);
				sw.WriteLine ("\t{0} partial class {1} : Attribute", this.Visibility, attrClassNameBase);
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
				sw.Close ();
				gen_info.Writer = null;
			}
		}
		
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
			var arr = sym as ArraySymbol;
			if (arr != null)
				return IsTypeCommensurate (opt, opt.SymbolTable.Lookup (arr.ElementType));
			if (sym is GenericSymbol)
				return sym.JavaName == "java.lang.Class";

			// outside Mono.Android.dll they are ManagedClassGen.
			if (sym is ClassGen)
				return sym.JavaName == "java.lang.Class" || sym.JavaName == "java.lang.String";

			return false;
		}

		public static string GetSignature (MethodBase method, CodeGenerationOptions opt)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (Parameter p in method.Parameters) {
				if (sb.Length > 0)
					sb.Append (", ");
				if (p.IsEnumified)
					sb.Append ("[global::Android.Runtime.GeneratedEnum] ");
				if (p.Annotation != null)
					sb.Append (p.Annotation);
				sb.Append (opt.GetOutputName (p.Type));
				sb.Append (" ");
				sb.Append (opt.GetSafeIdentifier (p.Name));
			}
			return sb.ToString ();
		}

		static IEnumerable<Method> GetAllMethods (GenBase t)
		{
			return t.Methods.Concat (t.Properties.Select (p => p.Getter)).Concat (t.Properties.Select (p => p.Setter).Where (m => m != null));
		}

		static string [] AutoDetectEnumifiedOverrideParameters (MethodBase method, CodeGenerationOptions opt)
		{
			if (method.Parameters.All (p => p.Type != "int"))
				return null;
			var classes = method.DeclaringType.Ancestors ().Concat (method.DeclaringType.Descendants (opt.Gens));
			classes = classes.Concat (classes.SelectMany(x => x.GetAllImplementedInterfaces ()));
			foreach (var t in classes) {
				foreach (var candidate in GetAllMethods (t).Where (m => m.Name == method.Name
					&& m.Parameters.Count == method.Parameters.Count
					&& m.Parameters.Any (p => p.IsEnumified))) {
					var ret = new string [method.Parameters.Count];
					bool mismatch = false;
					for (int i = 0; i < method.Parameters.Count; i++) {
						if (method.Parameters [i].Type == "int" && candidate.Parameters [i].IsEnumified)
							ret [i] = candidate.Parameters [i].Type;
						else if (method.Parameters [i].Type != candidate.Parameters [i].Type) {
							mismatch = true;
							break;
						}
					}
					if (mismatch)
						continue;
					for (int i = 0; i < ret.Length; i++)
						if (ret [i] != null)
							method.Parameters [i].SetGeneratedEnumType (ret [i]);
					return ret;
				}
			}
			return null;
		}

		static string AutoDetectEnumifiedOverrideReturn (Method method, CodeGenerationOptions opt)
		{
			if (method.RetVal.FullName != "int")
				return null;
			var classes = method.DeclaringType.Ancestors ().Concat (method.DeclaringType.Descendants (opt.Gens));
			classes = classes.Concat (classes.SelectMany(x => x.GetAllImplementedInterfaces ()));
			foreach (var t in classes) {
				foreach (var candidate in GetAllMethods (t).Where (m => m.Name == method.Name && m.Parameters.Count == method.Parameters.Count)) {
					if (method.JniSignature != candidate.JniSignature)
						continue;
					if (candidate.IsReturnEnumified)
						method.RetVal.SetGeneratedEnumType (candidate.RetVal.FullName);
				}
			}
			return null;
		}

		void AutoDetectEnumifiedOverrideProperties (Property prop, CodeGenerationOptions opt)
		{
			if (prop.Type != "int")
				return;
			var classes = prop.Getter.DeclaringType.Ancestors ().Concat (prop.Getter.DeclaringType.Descendants (opt.Gens));
			classes = classes.Concat (classes.SelectMany(x => x.GetAllImplementedInterfaces ()));
			foreach (var t in classes) {
				foreach (var candidate in t.Properties.Where (p => p.Name == prop.Name)) {
					if (prop.Getter.JniSignature != candidate.Getter.JniSignature)
						continue;
					if (candidate.Getter.IsReturnEnumified)
						prop.Getter.RetVal.SetGeneratedEnumType (candidate.Getter.RetVal.FullName);
				}
			}
		}
	}
}

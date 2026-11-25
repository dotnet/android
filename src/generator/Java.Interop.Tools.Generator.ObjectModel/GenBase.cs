using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Java.Interop.Tools.Generator;
using MonoDroid.Generation.Utilities;
using Xamarin.Android.Binder;

namespace MonoDroid.Generation
{
	public abstract class GenBase : IGeneratable, ApiVersionsSupport.IApiAvailability, ISourceLineInfo
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

		public int LineNumber { get; set; } = -1;
		public int LinePosition { get; set; } = -1;
		public string SourceFile { get; set; }

		public string ReturnCast => string.Empty;

		public JavadocInfo JavadocInfo { get; set; }

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
				if (gen.JavaName.StartsWith (nest.JavaName + ".", StringComparison.Ordinal)) {
					nest.AddNestedType (gen);
					return;
				}
			}

			var removes = new List<GenBase> ();

			foreach (var nest in NestedTypes) {
				if (nest.JavaName.StartsWith (gen.JavaName + ".", StringComparison.Ordinal)) {
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
						if (prop_hash.TryGetValue (prop_name, out var existing)) {
							if (m.Name.StartsWith ("Get", StringComparison.Ordinal))
								unmatched.Add (m);
							else {
								unmatched.Add (existing.Getter);
								existing.Getter = m;
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
				else if (prop_hash.TryGetValue (m.PropertyName, out var prop)) {
					Property baseProp = BaseGen?.Properties.FirstOrDefault (p => p.Name == m.PropertyName);
					if (prop.Getter.RetVal.FullName == m.Parameters [0].Type &&
							prop.Getter.IsAbstract == m.IsAbstract && // SearchIterator abstract getIndex() and non-abstract setIndex()
							prop.Getter.Visibility == m.Visibility &&
							(baseProp == null || baseProp.Setter != null) &&
							prop.Getter.SourceApiLevel <= m.SourceApiLevel)
						prop.Setter = m;
					else
						unmatched.Add (m);
				} else if (prop_hash.TryGetValue ("Is" + m.PropertyName, out prop)) {
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
				if (m.GenerateDispatchingSetter && prop_hash.TryGetValue (m.PropertyName, out var dispatchingProp))
					dispatchingProp.GenerateDispatchingSetter = true;
			}
			Methods = unmatched;
		}

		IEnumerable<GenBase> Ancestors ()
		{
			for (var g = BaseGen; g != null; g = g.BaseGen)
				yield return g;
		}

		public string AnnotatedVisibility => support.AnnotatedVisibility;

		// not: not currently assembly qualified, but it uses needed
		// Type.GetType() conventions such as '/' for nested types.
		public string AssemblyQualifiedName => string.IsNullOrWhiteSpace (Namespace)
			? $"{FullName.Replace ('.', '/')}"
			: $"{Namespace}." + $"{FullName.Substring (Namespace.Length + 1).Replace ('.', '/')}";

		public AndroidSdkVersion ApiAvailableSince { get; set; }

		public AndroidSdkVersion ApiRemovedSince { get; set; }

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
			if (jni_sig_hash.TryGetValue (method.JavaName + method.JniSignature, out var bm)) {
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

		public AndroidSdkVersion? DeprecatedSince => support.DeprecatedSince;

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
			// Process regular methods (non-static, non-interface default methods)
			foreach (var m in Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod)) {
				bool Matches (Method mm) =>
					mm.Name == m.Name &&
					mm.Visibility == m.Visibility &&
					mm.RetVal.FullName == m.RetVal.FullName && // if return type is different, it could be still "new", not "override".
					ParameterList.Equals (mm.Parameters, m.Parameters);

				for (var bt = GetBaseGen (opt); bt != null; bt = bt.GetBaseGen (opt)) {
					var bm = bt.Methods.FirstOrDefault (Matches);
					if (bm == null && bt is ClassGen { IsAbstract: true } abstractBase &&
							!bt.ContainsMethod (m, false, false)) {
						// These abstract slots are emitted from interfaces later, during class generation.
						// Skip methods that BoundClass emits as explicit implementations instead.
						bm = bt.GetAllDerivedInterfaces ().SelectMany (iface => iface.Methods).FirstOrDefault (mm =>
							!mm.IsInterfaceDefaultMethod && !mm.IsStatic && !mm.IsGeneric && !mm.RetVal.IsGeneric &&
							string.IsNullOrEmpty (mm.ExplicitInterface) &&
							!abstractBase.SkippedInterfaceMethods.Contains (mm.GetSkipInvokerSignature ()) &&
							!abstractBase.IsCovariantMethod (mm) &&
							Matches (mm));
					}
					if (bm == null) {
						continue;
					}

					m.IsOverride = true;
					if (opt.FixObsoleteOverrides) {
						// If method overrides a deprecated method, it also needs to be marked as deprecated
						if (bm.Deprecated.HasValue () && !m.Deprecated.HasValue ())
							m.Deprecated = bm.Deprecated;

						// Fix issue when base method was deprecated before the overriding method, set both both to base method value
						if (bm.DeprecatedSince.GetValueOrDefault (default) < m.DeprecatedSince.GetValueOrDefault (default))
							m.DeprecatedSince = bm.DeprecatedSince;
					}

					// If a "removed" method overrides a "not removed" method, the method was
					// likely moved to a base class, so don't mark it as removed.
					if (m.ApiRemovedSince > 0 && bm.ApiRemovedSince == 0) {
						m.ApiRemovedSince = default;
					}
					break;
				}
			}

			// A property accessor can correspond to a standalone method in a base class
			// when the accessor types differ. Compare the underlying Java methods.
			foreach (var prop in Properties) {
				foreach (var m in new [] { prop.Getter, prop.Setter }) {
					if (m == null || m.ApiRemovedSince == 0) {
						continue;
					}

					for (var bt = GetBaseGen (opt); bt != null; bt = bt.GetBaseGen (opt)) {
						var bm = bt.GetAllMethods ().FirstOrDefault (mm =>
							mm.ApiRemovedSince == 0 &&
							mm.JavaName == m.JavaName &&
							mm.Visibility == m.Visibility &&
							((m == prop.Getter &&
								mm.RetVal.FullName == m.RetVal.FullName &&
								ParameterList.Equals (mm.Parameters, m.Parameters)) ||
							(m == prop.Setter &&
								(ParameterList.Equals (mm.Parameters, m.Parameters) ||
									(mm.Parameters.Count == 1 && mm.Parameters [0].Symbol is GenericTypeParameter)))));
						if (bm != null) {
							m.ApiRemovedSince = default;
							break;
						}
					}
				}
			}

			// Process interface inheritance for both regular and default interface methods
			if (this is InterfaceGen currentInterface) {
				// For interfaces, check all base interfaces (interfaces that this interface implements/extends)
				var baseInterfaces = currentInterface.GetAllDerivedInterfaces ();

				foreach (var m in Methods.Where (m => !m.IsStatic)) {
					foreach (var baseIface in baseInterfaces) {
						var bm = baseIface.Methods.FirstOrDefault (mm =>
							mm.Name == m.Name &&
							mm.Visibility == m.Visibility &&
							mm.RetVal.FullName == m.RetVal.FullName &&
							ParameterList.Equals (mm.Parameters, m.Parameters));
						if (bm == null) {
							continue;
						}
						// If a "removed" interface method overrides a "not removed" method, the method was
						// likely moved to a base interface, so don't mark it as removed.
						if (m.ApiRemovedSince > 0 && bm.ApiRemovedSince == 0) {
							m.ApiRemovedSince = default;
						}
						break;
					}
				}

				// Process interface property getter/setter methods for ApiRemovedSince fixup
				foreach (var prop in Properties) {
					foreach (var baseIface in baseInterfaces) {
						var baseProp = baseIface.Properties.FirstOrDefault (p => p.Name == prop.Name && p.Type == prop.Type);
						if (baseProp == null)
							continue;

						bool shouldBreak = false;
						if (prop.Getter != null && prop.Getter.ApiRemovedSince > 0 && baseProp.Getter != null && baseProp.Getter.ApiRemovedSince == 0) {
							if (baseProp.Getter.Visibility == prop.Getter.Visibility &&
								ParameterList.Equals (baseProp.Getter.Parameters, prop.Getter.Parameters) &&
								baseProp.Getter.RetVal.FullName == prop.Getter.RetVal.FullName) {
								prop.Getter.ApiRemovedSince = default;
								shouldBreak = true;
							}
						}
						if (prop.Setter != null && prop.Setter.ApiRemovedSince > 0 && baseProp.Setter != null && baseProp.Setter.ApiRemovedSince == 0) {
							if (baseProp.Setter.Visibility == prop.Setter.Visibility &&
								ParameterList.Equals (baseProp.Setter.Parameters, prop.Setter.Parameters)) {
								prop.Setter.ApiRemovedSince = default;
								shouldBreak = true;
							}
						}
						if (shouldBreak) {
							break;
						}
					}
				}

				// Process interface field inheritance for ApiRemovedSince fixup
				foreach (var field in Fields) {
					foreach (var baseIface in baseInterfaces) {
						var baseField = baseIface.Fields.FirstOrDefault (f => f.Name == field.Name && f.TypeName == field.TypeName && f.Visibility == field.Visibility);
						if (baseField == null) {
							continue;
						}
						// If a "removed" interface field overrides a "not removed" field, the field was
						// likely moved to a base interface, so don't mark it as removed.
						if (field.ApiRemovedSince > 0 && baseField.ApiRemovedSince == 0) {
							field.ApiRemovedSince = default;
						}
						break;
					}
				}
			}

			// Interface default methods can be overriden. We want to process them differently.
			var checkDimOverrideTargets = opt.SupportDefaultInterfaceMethods ? Methods : Methods.Where (m => m.IsInterfaceDefaultMethod);

			// We need to check all the implemented interfaces of all the base types.
			var allIfaces = new List<InterfaceGen> ();

			for (var gen = this; gen != null; gen = gen.BaseGen)
				gen.GetAllDerivedInterfaces (allIfaces);

			foreach (var m in checkDimOverrideTargets.Where (m => !m.IsStatic)) {
				foreach (var bt in allIfaces.Distinct ()) {
					// We mark a method as an override if (1) it is a DIM, or (2) if the base method is DIM
					// (i.e. we don't mark as override if a class method "implements" normal iface method.)
					var bm = bt.Methods.FirstOrDefault (mm => (m.IsInterfaceDefaultMethod || !mm.IsAbstract) && mm.Name == m.Name && ParameterList.Equals (mm.Parameters, m.Parameters));

					if (bm != null) {
						m.OverriddenInterfaceMethod = bm;
						break;
					}
				}
			}

			foreach (var m in Methods) {
				if (m.Name == Name || ContainsProperty (m.Name, true) || HasNestedType (m.Name))
					m.Name = "Invoke" + m.Name;
				if ((m.Name == "ToString" && m.Parameters.Count == 0) || (BaseGen != null && BaseGen.ContainsMethod (m, false, false)))
					m.IsOverride = true;
			}

			foreach (var nt in NestedTypes)
				nt.FixupMethodOverrides (opt);
		}

		internal void FixupInterfaceMethodNullability ()
		{
			if (this is InterfaceGen) {
				foreach (var method in Methods.Where (m => m.IsInterfaceDefaultMethod && m.OverriddenInterfaceMethod != null)) {
					for (var contract = method.OverriddenInterfaceMethod; contract != null; contract = contract.OverriddenInterfaceMethod) {
						method.ReturnNotNull = method.RetVal.NotNull = method.RetVal.NotNull || contract.RetVal.NotNull;
						for (int i = 0; i < method.Parameters.Count; i++)
							method.Parameters [i].NotNull &= contract.Parameters [i].NotNull;
					}
				}
			}
			foreach (var nested in NestedTypes)
				nested.FixupInterfaceMethodNullability ();
		}

		public abstract string FromNative (CodeGenerationOptions opt, string varname, bool owned);

		public string FullName {
			get => support.FullName;
			set => support.FullName = value;
		}

		(object Package, object Name, string Id) javaFullNameIdCache;

		/// <summary>
		/// Fully-qualifed Java name, usable as a C# identifier, by replacing
		/// `.` and `$` with `_`.  Example: java_lang_Object.
		/// </summary>
		public string JavaFullNameId {
			get {
				if (object.ReferenceEquals (javaFullNameIdCache.Package, support.PackageName) &&
						object.ReferenceEquals (javaFullNameIdCache.Name, support.JavaSimpleName))
					return javaFullNameIdCache.Id;
				javaFullNameIdCache.Package = support.PackageName;
				javaFullNameIdCache.Name = support.JavaSimpleName;
				javaFullNameIdCache.Id =
					string.Concat (support.PackageName, support.PackageName == null ? null : "_", support.JavaSimpleName)
					.Replace ('.', '_')
					.Replace ('$', '_');
				return javaFullNameIdCache.Id;
			}
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
						sw.WriteLine ("\t\tpublic {0} {1} {{ get; set; }}", opt.GetTypeReferenceName (method.RetVal), method.Name);
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
			Methods.Concat (Properties.Select (p => p.Getter)).Concat (Properties.Select (p => p.Setter)).Where (m => m != null);

		GenBase GetBaseGen (CodeGenerationOptions opt)
		{
			if (this is InterfaceGen)
				return null;

			if (BaseGen != null)
				return BaseGen;

			if (BaseSymbol == null || opt?.SymbolTable == null)
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

		public string GetObjectHandleProperty (CodeGenerationOptions opt, string variable)
		{
			var handleType = IsThrowable () ? "Java.Lang.Throwable" : "Java.Lang.Object";

			return $"((global::{handleType}) {variable}).Handle";
		}

		public Property GetPropertyByName (string name, bool check_ifaces) =>
			GetPropertyByName (name, check_ifaces, true);

		public Property GetPropertyByName (string name, bool check_ifaces, bool check_base_ifaces)
		{
			if (prop_hash.TryGetValue (name, out var prop))
				return prop;

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

		public string JavaName => string.IsNullOrWhiteSpace (PackageName)
			? JavaSimpleName
			: $"{PackageName}.{JavaSimpleName}";

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
						Report.LogCodedWarning (0, Report.WarningBaseInterfaceNotFound, this, FullName, iface_name);
					else
						Report.LogCodedWarning (0, Report.WarningBaseInterfaceInvalid, this, FullName, iface_name);
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

			// If we can't validate a static or default interface method it's ok to ignore it and still bind the interface
			var method_cnt = Methods.Where (m => !m.IsInterfaceDefaultMethod && !m.IsStatic).Count ();

			Methods = Methods.Where (m => ValidateMethod (opt, m, context)).ToList ();
			MethodValidationFailed = method_cnt != Methods.Where (m => !m.IsInterfaceDefaultMethod && !m.IsStatic).Count ();

			foreach (Method m in Methods) {
				if (m.IsVirtual)
					HasVirtualMethods = true;
				if (m.Name == "HashCode" && m.Parameters.Count == 0) {
					m.IsOverride = true;
					m.Name = "GetHashCode";
				}
				jni_sig_hash [m.JavaName + m.JniSignature] = m;
				if ((m.Name == "ToString" && m.Parameters.Count == 0) || (BaseSymbol != null && BaseSymbol.ContainsMethod (m, false, false)))
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

			string format = "{0} {1} = {5}global::Java.Lang.Object.GetObject<{4}> ({2}, {3});";
			string transfer = "JniHandleOwnership.TransferLocalRef";
			string doNotTransfer = "JniHandleOwnership.DoNotTransfer";

			return new string []{
				string.Format (format,
					       "var",
					       opt.GetSafeIdentifier (var_name),
					       opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)),
					       owned ? transfer : doNotTransfer,
					       opt.GetOutputName (rgm != null ? (rgm.GetGenericJavaObjectTypeOverride () ?? FullName) : FullName),
					       rgm != null ? "(" + opt.GetOutputName (FullName) + opt.NullableOperator + ")" : string.Empty)
			};
		}

		public string [] PostCall (CodeGenerationOptions opt, string var_name) => new string [] { };

		public string [] PostCallback (CodeGenerationOptions opt, string var_name) => new string [] { };

		public string RawJniName => (string.IsNullOrWhiteSpace (PackageName) ? string.Empty : PackageName.Replace ('.', '/') + "/") + JavaSimpleName.Replace ('.', '$');

		public string RawVisibility => support.Visibility;

		public bool RequiresNew (Property property) =>
			RequiresNew (property, null);

		public bool RequiresNew (Property property, CodeGenerationOptions opt) =>
			RequiresNewForMemberName (property.AdjustedName, opt);

		public bool RequiresNew (Field field, CodeGenerationOptions opt = null) =>
			RequiresNewForMemberName (field.Name, opt);

		public bool RequiresNew (string memberName, Method method) =>
			RequiresNew (memberName, method, null);

		public bool RequiresNew (string memberName, Method method, CodeGenerationOptions opt) =>
			RequiresNewForMethod (memberName, GetParameterTypes (method.Parameters, opt, stringOverload: false), opt);

		internal bool RequiresNewStringOverload (Method method, CodeGenerationOptions opt) =>
			RequiresNewForMethod (method.Name, GetParameterTypes (method.Parameters, opt, stringOverload: true), opt);

		internal bool RequiresNewForMemberName (string memberName, CodeGenerationOptions opt)
		{
			if (string.IsNullOrEmpty (memberName))
				return false;

			if (this is InterfaceGen iface) {
				if (opt == null || !iface.IsConstSugar (opt)) {
					if (RequiresNewForJavaInterfaceMember (memberName))
						return true;
				}
				foreach (var baseIface in iface.GetEmittedBaseInterfaces (opt)) {
					if (baseIface.HasAccessibleEmittedMember (memberName, opt, includePrivate: false))
						return true;
				}
				return false;
			}

			if (RequiresNewForSystemObjectMember (memberName))
				return true;

			if (FullName != "Java.Lang.Object" && (!(this is ClassGen cls) || cls.InheritsObject)) {
				if (RequiresNewForJavaObjectHandwrittenMember (memberName))
					return true;
			}

			if (IsThrowable () && ThrowableRequiresNew.Contains (memberName))
				return true;

			for (var anc = GetBaseGen (opt); anc != null; anc = anc.GetBaseGen (opt)) {
				bool isEnclosing = anc.IsEnclosingTypeOf (this);
				if (anc.HasAccessibleEmittedMember (memberName, opt, includePrivate: isEnclosing))
					return true;
			}

			return false;
		}

		internal bool RequiresNewForMethod (string memberName, string [] paramTypes, CodeGenerationOptions opt)
		{
			if (string.IsNullOrEmpty (memberName))
				return false;

			if (this is InterfaceGen iface) {
				if (opt == null || !iface.IsConstSugar (opt)) {
					if (RequiresNewForJavaInterfaceMethod (memberName, paramTypes))
						return true;
				}
				foreach (var baseIface in iface.GetEmittedBaseInterfaces (opt)) {
					if (baseIface.HasAccessibleEmittedMemberForMethod (memberName, paramTypes, opt, includePrivate: false))
						return true;
				}
				return false;
			}

			if (RequiresNewForSystemObjectMethod (memberName, paramTypes))
				return true;

			if (FullName != "Java.Lang.Object" && (!(this is ClassGen cls) || cls.InheritsObject)) {
				if (RequiresNewForJavaObjectHandwrittenMethod (memberName, paramTypes))
					return true;
			}

			if (IsThrowable () && RequiresNewForThrowableMethod (memberName, paramTypes))
				return true;

			for (var anc = GetBaseGen (opt); anc != null; anc = anc.GetBaseGen (opt)) {
				bool isEnclosing = anc.IsEnclosingTypeOf (this);
				if (anc.HasAccessibleEmittedMemberForMethod (memberName, paramTypes, opt, includePrivate: isEnclosing))
					return true;
			}

			return false;
		}

		internal static bool RequiresNewForSystemObjectMember (string memberName)
		{
			switch (memberName) {
				case "GetHashCode":
				case "GetType":
				case "ToString":
				case "Equals":
				case "ReferenceEquals":
				case "MemberwiseClone":
					return true;
			}
			return false;
		}

		internal static bool RequiresNewForSystemObjectMethod (string memberName, string [] paramTypes)
		{
			switch (memberName) {
				case "GetHashCode":
				case "GetType":
				case "ToString":
				case "MemberwiseClone":
					return paramTypes.Length == 0;
				case "Equals":
					if (paramTypes.Length == 1 && paramTypes [0] == "object")
						return true;
					if (paramTypes.Length == 2 && paramTypes [0] == "object" && paramTypes [1] == "object")
						return true;
					return false;
				case "ReferenceEquals":
					return paramTypes.Length == 2 && paramTypes [0] == "object" && paramTypes [1] == "object";
			}
			return false;
		}

		static bool RequiresNewForJavaInterfaceMember (string memberName) =>
			JavaInterfacePropertyNames.Contains (memberName) || JavaInterfaceMethodNames.Contains (memberName);

		static bool RequiresNewForJavaInterfaceMethod (string memberName, string [] paramTypes)
		{
			if (JavaInterfacePropertyNames.Contains (memberName))
				return true;
			switch (memberName) {
				case "Dispose":
				case "UnregisterFromRuntime":
				case "DisposeUnlessReferenced":
				case "Disposed":
				case "Finalized":
					return paramTypes.Length == 0;
				case "SetJniIdentityHashCode":
					return paramTypes.Length == 1 && paramTypes [0] == "int";
				case "SetPeerReference":
					return paramTypes.Length == 1 && paramTypes [0] == "Java.Interop.JniObjectReference";
				case "SetJniManagedPeerState":
					return paramTypes.Length == 1 && paramTypes [0] == "Java.Interop.JniManagedPeerStates";
			}
			return false;
		}

		static bool RequiresNewForJavaObjectHandwrittenMember (string memberName) =>
			RequiresNewForJavaInterfaceMember (memberName) || JavaObjectExtraPropertyNames.Contains (memberName) || JavaObjectExtraMethodNames.Contains (memberName);

		static bool RequiresNewForJavaObjectHandwrittenMethod (string memberName, string [] paramTypes)
		{
			if (JavaInterfacePropertyNames.Contains (memberName) || JavaObjectExtraPropertyNames.Contains (memberName))
				return true;
			if (RequiresNewForJavaInterfaceMethod (memberName, paramTypes))
				return true;
			switch (memberName) {
				case "Dispose":
					return paramTypes.Length == 1 && paramTypes [0] == "bool";
				case "SetHandle":
					return paramTypes.Length == 2 && paramTypes [0] == "IntPtr" && paramTypes [1] == "Android.Runtime.JniHandleOwnership";
			}
			return false;
		}

		internal static bool RequiresNewForJavaObjectMember (string memberName, CodeGenerationOptions opt)
		{
			if (RequiresNewForSystemObjectMember (memberName) || RequiresNewForJavaObjectHandwrittenMember (memberName))
				return true;
			if (opt?.SymbolTable?.Lookup ("java.lang.Object") is ClassGen jlo)
				return jlo.HasAccessibleEmittedMember (memberName, opt, includePrivate: false);
			return false;
		}

		internal static bool RequiresNewForJavaObjectMethod (string memberName, Method method, CodeGenerationOptions opt, bool stringOverload = false)
		{
			var paramTypes = GetParameterTypes (method.Parameters, opt, stringOverload);
			if (RequiresNewForSystemObjectMethod (memberName, paramTypes) || RequiresNewForJavaObjectHandwrittenMethod (memberName, paramTypes))
				return true;
			if (opt?.SymbolTable?.Lookup ("java.lang.Object") is ClassGen jlo)
				return jlo.HasAccessibleEmittedMemberForMethod (memberName, paramTypes, opt, includePrivate: false);
			return false;
		}

		internal static bool CanOverrideJavaObjectMethod (string memberName, Method method, CodeGenerationOptions opt)
		{
			if (method.IsStatic)
				return false;

			var paramTypes = GetParameterTypes (method.Parameters, opt, stringOverload: false);
			var retType = NormalizeCSharpType (opt != null ? opt.GetOutputName (method.RetVal.FullName) : method.RetVal.FullName);

			// System.Object public virtual methods
			if (memberName == "GetHashCode" && paramTypes.Length == 0 && retType == "int")
				return true;
			if (memberName == "ToString" && paramTypes.Length == 0 && retType == "string")
				return true;
			if (memberName == "Equals" && paramTypes.Length == 1 && paramTypes [0] == "object" && retType == "bool")
				return true;

			// Java.Lang.Object public virtual/override methods (e.g., Equals(Java.Lang.Object?))
			if (opt?.SymbolTable?.Lookup ("java.lang.Object") is ClassGen jlo) {
				foreach (var m in jlo.Methods) {
					if (!m.IsValid || m.IsStatic || !string.IsNullOrEmpty (m.ExplicitInterface))
						continue;
					if (!string.Equals (m.Visibility, "public", StringComparison.OrdinalIgnoreCase))
						continue;
					if (!m.IsVirtual && !m.IsOverride)
						continue;
					if (m.AdjustedName != memberName)
						continue;
					var jloRetType = NormalizeCSharpType (opt.GetOutputName (m.RetVal.FullName));
					if (jloRetType != retType)
						continue;
					if (ParameterTypesMatch (GetParameterTypes (m.Parameters, opt, stringOverload: false), paramTypes))
						return true;
				}
			}

			return false;
		}

		static bool RequiresNewForThrowableMethod (string memberName, string [] paramTypes)
		{
			if (ThrowablePropertyNames.Contains (memberName))
				return true;
			switch (memberName) {
				case "GetBaseException":
					return paramTypes.Length == 0;
				case "GetObjectData":
					return paramTypes.Length == 2 &&
						paramTypes [0] == "System.Runtime.Serialization.SerializationInfo" &&
						paramTypes [1] == "System.Runtime.Serialization.StreamingContext";
			}
			return false;
		}

		internal bool IsEnclosingTypeOf (GenBase candidate)
		{
			if (NestedTypes.Count == 0)
				return false;
			foreach (var nt in NestedTypes) {
				if (ReferenceEquals (nt, candidate) || nt.IsEnclosingTypeOf (candidate))
					return true;
			}
			return false;
		}

		internal bool HasEnclosingAncestor (CodeGenerationOptions opt)
		{
			for (var anc = GetBaseGen (opt); anc != null; anc = anc.GetBaseGen (opt)) {
				if (anc.IsEnclosingTypeOf (this))
					return true;
			}
			return false;
		}

		internal IEnumerable<(GenBase owner, Method method, string propertyName, bool isFormatted)> GetEnclosingAncestorCallbackEntries (CodeGenerationOptions opt)
		{
			for (var anc = GetBaseGen (opt); anc != null; anc = anc.GetBaseGen (opt)) {
				if (!anc.IsEnclosingTypeOf (this))
					continue;
				foreach (var entry in anc.GetEmittedCallbackEntries (opt))
					yield return entry;
			}
		}

		IEnumerable<(GenBase owner, Method method, string propertyName, bool isFormatted)> GetEmittedCallbackEntries (CodeGenerationOptions opt)
		{
			bool isFinal = this is ClassGen cg && cg.IsFinal;
			foreach (var m in Methods) {
				if (!m.IsValid || m.IsStatic || !string.IsNullOrEmpty (m.ExplicitInterface))
					continue;
				bool isAbstract = m.IsAbstract && m.OverriddenInterfaceMethod == null && (opt == null || opt.SupportDefaultInterfaceMethods || !m.IsInterfaceDefaultMethod);
				if (isAbstract || (!isFinal && m.IsVirtual))
					yield return (this, m, null, m.IsReturnCharSequence);
			}
			foreach (var p in Properties) {
				if (!string.IsNullOrEmpty (p.ExplicitInterface) || p.Getter == null || p.Getter.IsStatic)
					continue;
				bool emitsCallback;
				if (p.Getter.IsAbstract) {
					var baseProp = BaseSymbol?.GetPropertyByName (p.Name, true);
					emitsCallback = baseProp == null;
				} else {
					emitsCallback = !isFinal && p.Getter.IsVirtual && (p.Setter == null || p.Setter.IsVirtual);
				}
				if (emitsCallback) {
					yield return (this, p.Getter, p.AdjustedName, false);
					if (p.Setter != null)
						yield return (this, p.Setter, p.AdjustedName, false);
				}
			}
			if (this is ClassGen ancCls && ancCls.IsAbstract) {
				foreach (var iface in ancCls.GetAllDerivedInterfaces ()) {
					foreach (var m in iface.Methods.Where (m => !m.IsInterfaceDefaultMethod && !m.IsStatic)) {
						if (ancCls.SkippedInterfaceMethods.Contains (m.GetSkipInvokerSignature ()))
							continue;
						if (ancCls.ExplicitlyImplementedInterfaceMethods.Contains (m.GetSignature ()))
							continue;
						if (m.RetVal.IsGeneric || !string.IsNullOrEmpty (m.ExplicitInterface))
							continue;
						bool alreadyInHierarchy = false;
						for (var c = ancCls; c != null; c = c.BaseGen) {
							if (c.ContainsMethod (m, false) || (c != ancCls && ancCls.ExplicitlyImplementedInterfaceMethods.Contains (m.GetSignature ()))) {
								alreadyInHierarchy = true;
								break;
							}
						}
						if (!alreadyInHierarchy)
							yield return (ancCls, m, null, m.IsReturnCharSequence);
					}
					foreach (var p in iface.Properties.Where (p => !p.Getter.IsInterfaceDefaultMethod && !p.Getter.IsStatic)) {
						if (ancCls.ContainsProperty (p.Name, false) || !string.IsNullOrEmpty (p.ExplicitInterface))
							continue;
						if (ancCls.BaseSymbol?.GetPropertyByName (p.Name, true) != null)
							continue;
						yield return (ancCls, p.Getter, p.AdjustedName, false);
						if (p.Setter != null)
							yield return (ancCls, p.Setter, p.AdjustedName, false);
					}
				}
			}
		}

		internal IEnumerable<InterfaceGen> GetEmittedBaseInterfaces (CodeGenerationOptions opt)
		{
			var visited = new HashSet<string> (StringComparer.Ordinal);
			var queue = new Queue<GenBase> ();
			queue.Enqueue (this);
			while (queue.Count > 0) {
				var cur = queue.Dequeue ();
				foreach (var isym in cur.Interfaces) {
					var igen = (isym is GenericSymbol gs ? gs.Gen : isym) as InterfaceGen;
					if (igen is null || (opt != null && igen.IsConstSugar (opt)) || igen.RawVisibility != "public")
						continue;
					if (visited.Add (igen.FullName)) {
						yield return igen;
						queue.Enqueue (igen);
					}
				}
			}
		}

		bool IsMemberAccessible (string visibility, bool includePrivate)
		{
			if (this is InterfaceGen)
				return true;
			return includePrivate || !string.Equals (visibility, "private", StringComparison.OrdinalIgnoreCase);
		}

		bool HasAccessibleEmittedNonMethod (string name, CodeGenerationOptions opt, bool includePrivate)
		{
			var fields = this is InterfaceGen iface
				? (opt != null ? iface.GetGeneratableFields (opt) : Fields)
				: Fields;

			foreach (var f in fields) {
				if (f.Name != name)
					continue;
				if (ContainsName (f.Name))
					continue;
				if (!IsMemberAccessible (f.Visibility, includePrivate))
					continue;
				return true;
			}

			foreach (var p in Properties) {
				if (!string.IsNullOrEmpty (p.ExplicitInterface))
					continue;
				if (this is InterfaceGen && opt != null && !opt.SupportDefaultInterfaceMethods && (p.Getter.IsStatic || p.Getter.IsInterfaceDefaultMethod))
					continue;
				var vis = this is InterfaceGen
					? "public"
					: (p.Getter.IsAbstract && p.Getter.RetVal.IsGeneric ? "protected" : (p.Setter ?? p.Getter).Visibility);
				if (!IsMemberAccessible (vis, includePrivate))
					continue;
				if (p.Getter.IsAbstract && this is ClassGen && BaseSymbol?.GetPropertyByName (p.Name, true) is Property bp && bp.Type != p.Getter.Return)
					continue;
				if (p.AdjustedName == name)
					return true;
				if (p.Type.StartsWith ("Java.Lang.ICharSequence", StringComparison.Ordinal) && p.Name == name)
					return true;
			}

			if (this is ClassGen cls && cls.IsAbstract) {
				foreach (var derivedIface in cls.GetAllDerivedInterfaces ()) {
					foreach (var p in derivedIface.Properties) {
						if (p.Getter.IsInterfaceDefaultMethod || p.Getter.IsStatic)
							continue;
						if (cls.ContainsProperty (p.Name, false) || !string.IsNullOrEmpty (p.ExplicitInterface))
							continue;
						if (cls.BaseSymbol?.GetPropertyByName (p.Name, true) is Property bp && bp.Type != p.Getter.Return)
							continue;
						if (p.AdjustedName == name)
							return true;
						if (p.Type.StartsWith ("Java.Lang.ICharSequence", StringComparison.Ordinal) && p.Name == name)
							return true;
					}
				}
			}

			foreach (var nt in NestedTypes) {
				if (this is InterfaceGen && nt.Unnest)
					continue;
				if (!IsMemberAccessible (nt.Visibility, includePrivate))
					continue;
				if (nt.Name == name)
					return true;
				if (this is ClassGen && nt is InterfaceGen nestedIface) {
					if (!nestedIface.NoAlternatives && (nestedIface.Fields.Any () || nestedIface.Methods.Any (m => m.IsStatic))) {
						var altName = nestedIface.HasManagedName ? nestedIface.Name.Substring (1) + "Consts" : nestedIface.Name.Substring (1);
						if (altName == name)
							return true;
						if (!nestedIface.HasManagedName && (opt == null || !opt.SupportInterfaceConstants) && nestedIface.Name.Substring (1) + "Consts" == name)
							return true;
					}
					if (nestedIface.IsListener) {
						foreach (var lm in nestedIface.Methods) {
							if (lm.EventName == string.Empty)
								continue;
							if (lm.RetVal.IsVoid || lm.IsEventHandlerWithHandledProperty) {
								if ((!lm.IsSimpleEventHandler || lm.IsEventHandlerWithHandledProperty) && nestedIface.GetArgsName (lm) == name)
									return true;
							} else if (nestedIface.GetEventDelegateName (lm) == name) {
								return true;
							}
						}
					}
				}
			}

			if (this is ClassGen listenerCls) {
				var connectorMethods = listenerCls.Methods.Concat (listenerCls.Properties.Where (p => p.Setter != null).Select (p => p.Setter));
				foreach (var m in connectorMethods) {
					if (!m.IsListenerConnector || m.EventName == string.Empty || m.ListenerType == null || !m.ListenerType.IsValid)
						continue;
					var baseEventName = m.CalculateEventName (listenerCls.ContainsName);
					if (string.IsNullOrEmpty (baseEventName))
						continue;
					var ltype = m.ListenerType;
					foreach (var lm in ltype.Methods) {
						if (lm.EventName == string.Empty)
							continue;
						var nameSpec = ltype.Methods.Count > 1 ? lm.EventName ?? lm.AdjustedName : string.Empty;
						var nameUnique = string.IsNullOrEmpty (nameSpec) ? baseEventName : nameSpec;
						if (nameUnique.StartsWith ("On", StringComparison.Ordinal))
							nameUnique = nameUnique.Substring (2);
						if (listenerCls.ContainsName (nameUnique))
							nameUnique += "Event";
						if (nameUnique == name)
							return true;
					}
				}
			}

			return false;
		}

		bool HasAccessibleEmittedMember (string name, CodeGenerationOptions opt, bool includePrivate)
		{
			if (HasAccessibleEmittedNonMethod (name, opt, includePrivate))
				return true;

			if (this is ClassGen && name == "GetEnumerator" && Interfaces.Any (p => p.FullName == "Java.Lang.ICharSequence"))
				return true;

			foreach (var (m, isAbstractDecl) in GetEmittedMethods (opt, includePrivate)) {
				if (m.AdjustedName == name)
					return true;
				if (MethodEmitsStringOverload (m, isAbstractDecl, opt) && m.Name == name)
					return true;
				if (m.Asyncify && m.AdjustedName + "Async" == name)
					return true;
			}

			return false;
		}

		bool HasAccessibleEmittedMemberForMethod (string name, string [] paramTypes, CodeGenerationOptions opt, bool includePrivate)
		{
			if (HasAccessibleEmittedNonMethod (name, opt, includePrivate))
				return true;

			if (this is ClassGen && name == "GetEnumerator" && paramTypes.Length == 0 && Interfaces.Any (p => p.FullName == "Java.Lang.ICharSequence"))
				return true;

			foreach (var (m, isAbstractDecl) in GetEmittedMethods (opt, includePrivate)) {
				if (m.AdjustedName == name && ParameterTypesMatch (GetParameterTypes (m.Parameters, opt, stringOverload: false), paramTypes))
					return true;
				if (MethodEmitsStringOverload (m, isAbstractDecl, opt) && m.Name == name && ParameterTypesMatch (GetParameterTypes (m.Parameters, opt, stringOverload: true), paramTypes))
					return true;
				if (m.Asyncify && m.AdjustedName + "Async" == name && ParameterTypesMatch (GetParameterTypes (m.Parameters, opt, stringOverload: false), paramTypes))
					return true;
			}

			return false;
		}

		bool MethodEmitsStringOverload (Method m, bool isAbstractDecl, CodeGenerationOptions opt)
		{
			if (this is InterfaceGen && !m.IsStatic && !m.IsInterfaceDefaultMethod)
				return false;
			if (isAbstractDecl)
				return m.IsReturnCharSequence || m.Parameters.HasCharSequence;
			var name_and_jnisig = m.JavaName + m.JniSignature.Replace ("java/lang/CharSequence", "java/lang/String");
			var gen_string_overload = !m.IsOverride && m.Parameters.HasCharSequence && !ContainsMethod (name_and_jnisig);
			return gen_string_overload || m.IsReturnCharSequence;
		}

		IEnumerable<(Method method, bool isAbstractDecl)> GetEmittedMethods (CodeGenerationOptions opt, bool includePrivate)
		{
			if (this is InterfaceGen) {
				foreach (var m in Methods) {
					if (!m.IsValid || !string.IsNullOrEmpty (m.ExplicitInterface))
						continue;
					bool isDimOrStatic = m.IsStatic || m.IsInterfaceDefaultMethod;
					if (isDimOrStatic) {
						if (opt == null || !opt.SupportDefaultInterfaceMethods)
							continue;
						if (m.OverriddenInterfaceMethod != null)
							continue; // emitted as explicit interface implementation
					}
					yield return (m, false);
				}
				yield break;
			}

			var cls = this as ClassGen;
			foreach (var m in Methods) {
				if (!m.IsValid || !string.IsNullOrEmpty (m.ExplicitInterface))
					continue;
				if (!IsMemberAccessible (m.Visibility, includePrivate))
					continue;
				bool isAbstractDecl = m.IsAbstract && m.OverriddenInterfaceMethod == null && (opt == null || opt.SupportDefaultInterfaceMethods || !m.IsInterfaceDefaultMethod);
				yield return (m, isAbstractDecl);
			}

			if (cls != null && opt != null && !opt.SupportDefaultInterfaceMethods) {
				var defaultMethods = cls.GetAllDerivedInterfaces ()
					.SelectMany (i => i.Methods)
					.Where (m => m.IsInterfaceDefaultMethod)
					.Where (m => !cls.ContainsMethod (m, false, false))
					.ToList ();
				var overrides = defaultMethods.Where (m => m.OverriddenInterfaceMethod != null).ToList ();
				var overridens = defaultMethods.Where (m => overrides.Where (_ => _.Name == m.Name && _.JniSignature == m.JniSignature)
					.Any (mm => mm.DeclaringType.GetAllDerivedInterfaces ().Contains (m.DeclaringType)));
				foreach (var m in defaultMethods.Except (overridens).Where (m => m.DeclaringType.IsGeneratable)) {
					if (!m.IsValid || !string.IsNullOrEmpty (m.ExplicitInterface))
						continue;
					if (!IsMemberAccessible (m.Visibility, includePrivate))
						continue;
					yield return (m, false);
				}
			}

			if (cls != null && cls.IsAbstract) {
				var emittedSigs = new HashSet<string> (StringComparer.Ordinal);
				foreach (var iface in cls.GetAllDerivedInterfaces ()) {
					foreach (var m in iface.Methods.Where (m => !m.IsInterfaceDefaultMethod && !m.IsStatic)) {
						if (cls.SkippedInterfaceMethods.Contains (m.GetSkipInvokerSignature ()))
							continue;
						var sig = m.GetSignature ();
						if (cls.ExplicitlyImplementedInterfaceMethods.Contains (sig))
							continue;
						if (m.RetVal.IsGeneric || !string.IsNullOrEmpty (m.ExplicitInterface))
							continue;
						if (!emittedSigs.Add (m.Name + "\0" + m.JniSignature))
							continue;
						bool alreadyInHierarchy = false;
						for (var c = cls; c != null; c = c.BaseGen) {
							if (c.ContainsMethod (m, false) || (c != cls && cls.ExplicitlyImplementedInterfaceMethods.Contains (sig))) {
								alreadyInHierarchy = true;
								break;
							}
						}
						if (!alreadyInHierarchy)
							yield return (m, true);
					}
				}
			}
		}

		internal static string [] GetParameterTypes (ParameterList parameters, CodeGenerationOptions opt, bool stringOverload)
		{
			if (parameters == null || parameters.Count == 0)
				return [];
			var result = new string [parameters.Count];
			for (int i = 0; i < parameters.Count; i++) {
				var t = opt != null ? opt.GetOutputName (parameters [i].InternalType) : parameters [i].InternalType;
				if (stringOverload)
					t = t.Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string");
				result [i] = NormalizeCSharpType (t);
			}
			return result;
		}

		static bool ParameterTypesMatch (string [] a, string [] b)
		{
			if (a.Length != b.Length)
				return false;
			for (int i = 0; i < a.Length; i++) {
				if (a [i] != b [i])
					return false;
			}
			return true;
		}

		static string NormalizeCSharpType (string typeName)
		{
			if (string.IsNullOrEmpty (typeName))
				return "";
			if (typeName.StartsWith ("global::", StringComparison.Ordinal))
				typeName = typeName.Substring ("global::".Length);
			if (typeName.EndsWith ("[]", StringComparison.Ordinal))
				return NormalizeCSharpType (typeName.Substring (0, typeName.Length - 2)) + "[]";
			switch (typeName) {
				case "System.Boolean": return "bool";
				case "System.Byte": return "byte";
				case "System.SByte": return "sbyte";
				case "System.Char": return "char";
				case "System.Int16": return "short";
				case "System.UInt16": return "ushort";
				case "System.Int32": return "int";
				case "System.UInt32": return "uint";
				case "System.Int64": return "long";
				case "System.UInt64": return "ulong";
				case "System.Single": return "float";
				case "System.Double": return "double";
				case "System.String": return "string";
				case "System.Object": return "object";
				case "System.Void": return "void";
				case "System.IntPtr": return "IntPtr";
				case "System.UIntPtr": return "UIntPtr";
				default: return typeName;
			}
		}

		static readonly HashSet<string> JavaInterfacePropertyNames = new HashSet<string> (StringComparer.Ordinal) {
			"Handle",
			"PeerReference",
			"JniPeerMembers",
			"JniManagedPeerState",
		};

		static readonly HashSet<string> JavaInterfaceMethodNames = new HashSet<string> (StringComparer.Ordinal) {
			"Dispose",
			"SetJniIdentityHashCode",
			"SetPeerReference",
			"SetJniManagedPeerState",
			"UnregisterFromRuntime",
			"DisposeUnlessReferenced",
			"Disposed",
			"Finalized",
		};

		static readonly HashSet<string> JavaObjectExtraPropertyNames = new HashSet<string> (StringComparer.Ordinal) {
			"JniIdentityHashCode",
			"ThresholdClass",
			"ThresholdType",
		};

		static readonly HashSet<string> JavaObjectExtraMethodNames = new HashSet<string> (StringComparer.Ordinal) {
			"SetHandle",
		};

		static readonly HashSet<string> ThrowablePropertyNames = new HashSet<string> (
			typeof (System.Exception).GetProperties ().Select (p => p.Name),
			StringComparer.Ordinal);

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

		public HashSet<string> SkippedInvokerMethods => support.SkippedInvokerMethods;

		public void StripNonBindables (CodeGenerationOptions opt)
		{
			// Strip out default interface methods if not desired
			if (!opt.SupportDefaultInterfaceMethods)
				Methods = Methods.Where (m => !m.IsInterfaceDefaultMethod).ToList ();

			NestedTypes = NestedTypes.Where (n => !n.IsObfuscated && n.Visibility != "private").ToList ();

			foreach (var n in NestedTypes)
				n.StripNonBindables (opt);
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

		// Prior to DIM, interfaces could not contain nested types so we generated them
		// as sibling types.  When DIM is enabled we can now generate them properly nested.
		// However this is an API break for existing bindings.  Setting this property
		// to true opts this interface into the sibling compatibility behavior.
		public bool Unnest { get; set; }

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

		public string Visibility {
			get => string.IsNullOrEmpty (support.Visibility) ? "public" : support.Visibility;
			set => support.Visibility = value;
		}
	}
}

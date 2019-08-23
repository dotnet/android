using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Binder;

namespace MonoDroid.Generation
{
	abstract class CodeGenerator
	{
		protected TextWriter writer;
		protected CodeGenerationOptions opt;

		public CodeGeneratorContext Context { get; } = new CodeGeneratorContext ();

		protected CodeGenerator (TextWriter writer, CodeGenerationOptions options)
		{
			this.writer = writer;
			opt = options;
		}

		internal virtual string GetAllInterfaceImplements () => "IJavaObject";

		internal abstract void WriteClassHandle (ClassGen type, string indent, bool requireNew);

		internal abstract void WriteClassHandle (InterfaceGen type, string indent, string declaringType);

		internal abstract void WriteClassInvokerHandle (ClassGen type, string indent, string declaringType);
		internal abstract void WriteInterfaceInvokerHandle (InterfaceGen type, string indent, string declaringType);

		internal abstract void WriteConstructorIdField (Ctor ctor, string indent);
		internal abstract void WriteConstructorBody (Ctor ctor, string indent, StringCollection call_cleanup);

		internal abstract void WriteMethodIdField (Method method, string indent);
		internal abstract void WriteMethodBody (Method method, string indent, GenBase type);

		internal abstract void WriteFieldIdField (Field field, string indent);
		internal abstract void WriteFieldGetBody (Field field, string indent, GenBase type);
		internal abstract void WriteFieldSetBody (Field field, string indent, GenBase type);

		public void WriteClass (ClassGen @class, string indent, GenerationInfo gen_info)
		{
			Context.ContextTypes.Push (@class);
			Context.ContextGeneratedMethods = new List<Method> ();

			gen_info.TypeRegistrations.Add (new KeyValuePair<string, string> (@class.RawJniName, @class.AssemblyQualifiedName));
			bool is_enum = @class.base_symbol != null && @class.base_symbol.FullName == "Java.Lang.Enum";
			if (is_enum)
				gen_info.Enums.Add (@class.RawJniName.Replace ('/', '.') + ":" + @class.Namespace + ":" + @class.JavaSimpleName);
			StringBuilder sb = new StringBuilder ();
			foreach (ISymbol isym in @class.Interfaces) {
				GenericSymbol gs = isym as GenericSymbol;
				InterfaceGen gen = (gs == null ? isym : gs.Gen) as InterfaceGen;
				if (gen != null && (gen.IsConstSugar || gen.RawVisibility != "public"))
					continue;
				if (sb.Length > 0)
					sb.Append (", ");
				sb.Append (opt.GetOutputName (isym.FullName));
			}

			string obj_type = null;
			if (@class.base_symbol != null) {
				GenericSymbol gs = @class.base_symbol as GenericSymbol;
				obj_type = gs != null && gs.IsConcrete ? gs.GetGenericType (null) : opt.GetOutputName (@class.base_symbol.FullName);
			}

			writer.WriteLine ("{0}// Metadata.xml XPath class reference: path=\"{1}\"", indent, @class.MetadataXPathReference);

			if (@class.IsDeprecated)
				writer.WriteLine ("{0}[ObsoleteAttribute (@\"{1}\")]", indent, @class.DeprecatedComment);
			writer.WriteLine ("{0}[global::Android.Runtime.Register (\"{1}\", DoNotGenerateAcw=true{2})]", indent, @class.RawJniName, @class.AdditionalAttributeString ());
			if (@class.TypeParameters != null && @class.TypeParameters.Any ())
				writer.WriteLine ("{0}{1}", indent, @class.TypeParameters.ToGeneratedAttributeString ());
			string inherits = "";
			if (@class.InheritsObject && obj_type != null) {
				inherits = ": " + obj_type;
			}
			if (sb.Length > 0) {
				if (string.IsNullOrEmpty (inherits))
					inherits = ": ";
				else
					inherits += ", ";
			}
			writer.WriteLine ("{0}{1} {2}{3}{4}partial class {5} {6}{7} {{",
					indent,
					@class.Visibility,
					@class.NeedsNew ? "new " : String.Empty,
					@class.IsAbstract ? "abstract " : String.Empty,
					@class.IsFinal ? "sealed " : String.Empty,
					@class.Name,
					inherits,
					sb.ToString ());
			writer.WriteLine ();

			var seen = new HashSet<string> ();
			WriteFields (@class.Fields, indent + "\t", @class, seen);
			bool haveNested = false;
			foreach (var iface in @class.GetAllImplementedInterfaces ()
					.Except (@class.BaseGen == null
						? new InterfaceGen [0]
						: @class.BaseGen.GetAllImplementedInterfaces ())
					.Where (i => i.Fields.Count > 0)) {
				if (!haveNested) {
					writer.WriteLine ();
					writer.WriteLine ("{0}\tpublic static class InterfaceConsts {{", indent);
					haveNested = true;
				}
				writer.WriteLine ();
				writer.WriteLine ("{0}\t\t// The following are fields from: {1}", indent, iface.JavaName);
				WriteFields (iface.Fields, indent + "\t\t", iface, seen);
			}

			if (haveNested) {
				writer.WriteLine ("{0}\t}}", indent);
				writer.WriteLine ();
			}

			foreach (GenBase nest in @class.NestedTypes) {
				if (@class.BaseGen != null && @class.BaseGen.ContainsNestedType (nest))
					if (nest is ClassGen)
						(nest as ClassGen).NeedsNew = true;
				WriteType (nest, indent + "\t", gen_info);
				writer.WriteLine ();
			}

			bool requireNew = @class.InheritsObject;
			if (!requireNew) {
				for (var bg = @class.BaseGen; bg != null && bg is ClassGen classGen && classGen.FromXml; bg = bg.BaseGen) {
					if (bg.InheritsObject) {
						requireNew = true;
						break;
					}
				}
			}
			WriteClassHandle (@class, indent, requireNew);

			WriteClassConstructors (@class, indent + "\t");

			WriteImplementedProperties (@class.Properties, indent + "\t", @class.IsFinal, @class);
			WriteClassMethods (@class, indent + "\t");

			if (@class.IsAbstract)
				WriteClassAbstractMembers (@class, indent + "\t");

			bool is_char_seq = false;
			foreach (ISymbol isym in @class.Interfaces) {
				if (isym is GenericSymbol) {
					GenericSymbol gs = isym as GenericSymbol;
					if (gs.IsConcrete) {
						// FIXME: not sure if excluding default methods is a valid idea...
						foreach (Method m in gs.Gen.Methods) {
							if (m.IsInterfaceDefaultMethod || m.IsStatic)
								continue;
							if (m.IsGeneric)
								WriteMethodExplicitIface (m, indent + "\t", gs);
						}

						var adapter = gs.Gen.AssemblyQualifiedName + "Invoker";
						foreach (Property p in gs.Gen.Properties) {
							if (p.Getter != null) {
								if (p.Getter.IsInterfaceDefaultMethod || p.Getter.IsStatic)
									continue;
							}
							if (p.Setter != null) {
								if (p.Setter.IsInterfaceDefaultMethod || p.Setter.IsStatic)
									continue;
							}
							if (p.IsGeneric)
								WritePropertyExplicitInterface (p, indent + "\t", gs, adapter);
						}
					}
				} else if (isym.FullName == "Java.Lang.ICharSequence")
					is_char_seq = true;
			}

			if (is_char_seq)
				WriteCharSequenceEnumerator (indent + "\t");

			writer.WriteLine (indent + "}");

			if (!@class.AssemblyQualifiedName.Contains ('/')) {
				foreach (InterfaceExtensionInfo nestedIface in @class.GetNestedInterfaceTypes ())
					WriteInterfaceExtensionsDeclaration (nestedIface.Type, indent, nestedIface.DeclaringType);
			}

			if (@class.IsAbstract) {
				writer.WriteLine ();
				WriteClassInvoker (@class, indent);
			}

			Context.ContextGeneratedMethods.Clear ();

			Context.ContextTypes.Pop ();
		}

		public void WriteClassAbstractMembers (ClassGen @class, string indent)
		{
			foreach (InterfaceGen gen in @class.GetAllDerivedInterfaces ())
				WriteInterfaceAbstractMembers (gen, @class, indent);
		}

		public void WriteClassConstructors (ClassGen @class, string indent)
		{
			if (@class.FullName != "Java.Lang.Object" && @class.InheritsObject) {
				writer.WriteLine ("{0}{1} {2} (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {{}}", indent, @class.IsFinal ? "internal" : "protected", @class.Name);
				writer.WriteLine ();
			}

			foreach (Ctor ctor in @class.Ctors) {
				if (@class.IsFinal && ctor.Visibility == "protected")
					continue;
				ctor.Name = @class.Name;
				WriteConstructor (ctor, indent, @class.InheritsObject, @class);
			}
		}

		public void WriteClassInvoker (ClassGen @class, string indent)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (InterfaceGen igen in @class.GetAllDerivedInterfaces ())
				if (igen.IsGeneric)
					sb.Append (", " + opt.GetOutputName (igen.FullName));
			writer.WriteLine ("{0}[global::Android.Runtime.Register (\"{1}\", DoNotGenerateAcw=true{2})]", indent, @class.RawJniName, @class.AdditionalAttributeString ());
			writer.WriteLine ("{0}internal partial class {1}Invoker : {1}{2} {{", indent, @class.Name, sb.ToString ());
			writer.WriteLine ();
			writer.WriteLine ("{0}\tpublic {1}Invoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {{}}", indent, @class.Name);
			writer.WriteLine ();
			WriteClassInvokerHandle (@class, indent + "\t", @class.Name + "Invoker");

			HashSet<string> members = new HashSet<string> ();
			WriteClassInvokerMembers (@class, indent + "\t", members);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteClassInvokerMembers (ClassGen @class, string indent, HashSet<string> members)
		{
			WriteClassPropertyInvokers (@class, @class.Properties, indent, members);
			WriteClassMethodInvokers (@class, @class.Methods, indent, members, null);

			foreach (InterfaceGen iface in @class.GetAllDerivedInterfaces ()) {
				//				if (iface.IsGeneric)
				//					continue;
				WriteClassPropertyInvokers (@class, iface.Properties.Where (p => !@class.ContainsProperty (p.Name, false, false)), indent, members);
				WriteClassMethodInvokers (@class, iface.Methods.Where (m => (opt.SupportDefaultInterfaceMethods || !m.IsInterfaceDefaultMethod) && !@class.ContainsMethod (m, false, false) && !@class.IsCovariantMethod (m) && !@class.ExplicitlyImplementedInterfaceMethods.Contains (m.GetSignature ())), indent, members, iface);
			}

			if (@class.BaseGen != null && @class.BaseGen.FullName != "Java.Lang.Object")
				WriteClassInvokerMembers (@class.BaseGen, indent, members);
		}

		public void WriteClassMethodInvokers (ClassGen @class, IEnumerable<Method> methods, string indent, HashSet<string> members, InterfaceGen gen)
		{
			foreach (Method m in methods) {
				string sig = m.GetSignature ();
				if (members.Contains (sig))
					continue;
				members.Add (sig);
				if (!m.IsAbstract)
					continue;
				if (@class.IsExplicitlyImplementedMethod (sig)) {
					// sw.WriteLine ("// This invoker explicitly implements this method");
					WriteMethodExplicitInterfaceInvoker (m, indent, gen);
				} else {
					// sw.WriteLine ("// This invoker overrides {0} method", gen.FullName);
					m.IsOverride = true;
					WriteMethod (m, indent, @class, false);
					m.IsOverride = false;
				}
			}
		}

		public void WriteClassMethods (ClassGen @class, string indent)
		{
			var methodsToDeclare = @class.Methods.AsEnumerable ();

			// This does not exclude overrides (unlike virtual methods) because we're not sure
			// if calling the base interface default method via JNI expectedly dispatches to
			// the derived method.
			var defaultMethods = @class.GetAllDerivedInterfaces ()
				.SelectMany (i => i.Methods)
				.Where (m => m.IsInterfaceDefaultMethod)
				.Where (m => !@class.ContainsMethod (m, false, false));
			var overrides = defaultMethods.Where (m => m.OverriddenInterfaceMethod != null);

			var overridens = defaultMethods.Where (m => overrides.Where (_ => _.Name == m.Name && _.JniSignature == m.JniSignature)
				.Any (mm => mm.DeclaringType.GetAllDerivedInterfaces ().Contains (m.DeclaringType)));

			methodsToDeclare = opt.SupportDefaultInterfaceMethods ? methodsToDeclare : methodsToDeclare.Concat (defaultMethods.Except (overridens)).Where (m => m.DeclaringType.IsGeneratable);

			foreach (var m in methodsToDeclare) {
				bool virt = m.IsVirtual;
				m.IsVirtual = !@class.IsFinal && virt;
				if (m.IsAbstract && m.OverriddenInterfaceMethod == null && (opt.SupportDefaultInterfaceMethods || !m.IsInterfaceDefaultMethod))
					WriteMethodAbstractDeclaration (m, indent, null, @class);
				else
					WriteMethod (m, indent, @class, true);
				Context.ContextGeneratedMethods.Add (m);
				m.IsVirtual = virt;
			}

			var methods = @class.Methods.Concat (@class.Properties.Where (p => p.Setter != null).Select (p => p.Setter));
			foreach (InterfaceGen type in methods.Where (m => m.IsListenerConnector && m.EventName != String.Empty).Select (m => m.ListenerType).Distinct ()) {
				writer.WriteLine ("#region \"Event implementation for {0}\"", type.FullName);
				WriteInterfaceListenerEventsAndProperties (type, indent, @class);
				writer.WriteLine ("#endregion");
			}
		}

		public void WriteImplementedProperties (IEnumerable<Property> targetProperties, string indent, bool isFinal, GenBase gen)
		{
			foreach (var prop in targetProperties) {
				bool get_virt = prop.Getter.IsVirtual;
				bool set_virt = prop.Setter == null ? false : prop.Setter.IsVirtual;
				prop.Getter.IsVirtual = !isFinal && get_virt;
				if (prop.Setter != null)
					prop.Setter.IsVirtual = !isFinal && set_virt;
				if (prop.Getter.IsAbstract)
					WritePropertyAbstractDeclaration (prop, indent, gen);
				else
					WriteProperty (prop, gen, indent);
				prop.Getter.IsVirtual = get_virt;
				if (prop.Setter != null)
					prop.Setter.IsVirtual = set_virt;
			}
		}

		public void WriteClassPropertyInvokers (ClassGen @class, IEnumerable<Property> properties, string indent, HashSet<string> members)
		{
			foreach (Property prop in properties) {
				if (members.Contains (prop.Name))
					continue;
				members.Add (prop.Name);
				if ((prop.Getter != null && !prop.Getter.IsAbstract) ||
						(prop.Setter != null && !prop.Setter.IsAbstract))
					continue;

				WriteProperty (prop, @class, indent, false, true);
			}
		}

		internal void WriteCharSequenceEnumerator (string indent)
		{
			writer.WriteLine ("{0}System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()", indent);
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn GetEnumerator ();", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}public System.Collections.Generic.IEnumerator<char> GetEnumerator ()", indent);
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\tfor (int i = 0; i < Length(); i++)", indent);
			writer.WriteLine ("{0}\t\tyield return CharAt (i);", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		internal virtual void WriteConstructor (Ctor constructor, string indent, bool useBase, ClassGen type)
		{
			string jni_sig = constructor.JniSignature;
			bool gen_string_overload = constructor.Parameters.HasCharSequence && !type.ContainsCtor (jni_sig.Replace ("java/lang/CharSequence", "java/lang/String"));
			System.Collections.Specialized.StringCollection call_cleanup = constructor.Parameters.GetCallCleanup (opt);
			WriteConstructorIdField (constructor, indent);
			writer.WriteLine ("{0}// Metadata.xml XPath constructor reference: path=\"{1}/constructor[@name='{2}'{3}]\"", indent, type.MetadataXPathReference, type.JavaSimpleName, constructor.Parameters.GetMethodXPathPredicate ());
			writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, ".ctor", jni_sig, String.Empty, constructor.AdditionalAttributeString ());
			if (constructor.Deprecated != null)
				writer.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, constructor.Deprecated.Replace ("\"", "\"\""));

			if (constructor.CustomAttributes != null)
				writer.WriteLine ("{0}{1}", indent, constructor.CustomAttributes);
			if (constructor.Annotation != null)
				writer.WriteLine ("{0}{1}", indent, constructor.Annotation);

			writer.WriteLine ("{0}{1} unsafe {2} ({3})", indent, constructor.Visibility, constructor.Name, constructor.GetSignature (opt));
			writer.WriteLine ("{0}\t: {1} (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)", indent, useBase ? "base" : "this");
			writer.WriteLine ("{0}{{", indent);
			WriteConstructorBody (constructor, indent + "\t", call_cleanup);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			if (gen_string_overload) {
				writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, ".ctor", jni_sig, String.Empty, constructor.AdditionalAttributeString ());
				writer.WriteLine ("{0}{1} unsafe {2} ({3})", indent, constructor.Visibility, constructor.Name, constructor.GetSignature (opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));
				writer.WriteLine ("{0}\t: {1} (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)", indent, useBase ? "base" : "this");
				writer.WriteLine ("{0}{{", indent);
				WriteConstructorBody (constructor, indent + "\t", call_cleanup);
				writer.WriteLine ("{0}}}", indent);
				writer.WriteLine ();
			}
		}

		public bool WriteFields (List<Field> fields, string indent, GenBase gen, HashSet<string> seen = null)
		{
			bool needsProperty = false;
			foreach (Field f in fields) {
				if (gen.ContainsName (f.Name)) {
					Report.Warning (0, Report.WarningFieldNameCollision, "Skipping {0}.{1}, due to a duplicate field, method or nested type name. {2} (Java type: {3})", gen.FullName, f.Name, gen.HasNestedType (f.Name) ? "(Nested type)" : gen.ContainsProperty (f.Name, false) ? "(Property)" : "(Method)", gen.JavaName);
					continue;
				}
				if (seen != null && seen.Contains (f.Name)) {
					Report.Warning (0, Report.WarningDuplicateField, "Skipping {0}.{1}, due to a duplicate field. (Field) (Java type: {2})", gen.FullName, f.Name, gen.JavaName);
					continue;
				}
				if (f.Validate (opt, gen.TypeParameters, Context)) {
					if (seen != null)
						seen.Add (f.Name);
					needsProperty = needsProperty || f.NeedsProperty;
					writer.WriteLine ();
					WriteField (f, indent, gen);
				}
			}
			return needsProperty;
		}

		internal virtual void WriteField (Field field, string indent, GenBase type)
		{
			if (field.IsEnumified)
				writer.WriteLine ("[global::Android.Runtime.GeneratedEnum]");
			if (field.NeedsProperty) {
				string fieldType = field.Symbol.IsArray ? "IList<" + field.Symbol.ElementType + ">" : opt.GetOutputName (field.Symbol.FullName);
				WriteFieldIdField (field, indent);
				writer.WriteLine ();
				writer.WriteLine ("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, type.MetadataXPathReference, field.JavaName);
				writer.WriteLine ("{0}[Register (\"{1}\"{2})]", indent, field.JavaName, field.AdditionalAttributeString ());
				writer.WriteLine ("{0}{1} {2}{3} {4} {{", indent, field.Visibility, field.IsStatic ? "static " : String.Empty, fieldType, field.Name);
				writer.WriteLine ("{0}\tget {{", indent);
				WriteFieldGetBody (field, indent + "\t\t", type);
				writer.WriteLine ("{0}\t}}", indent);

				if (!field.IsConst) {
					writer.WriteLine ("{0}\tset {{", indent);
					WriteFieldSetBody (field, indent + "\t\t", type);
					writer.WriteLine ("{0}\t}}", indent);
				}
				writer.WriteLine ("{0}}}", indent);
			} else {
				writer.WriteLine ("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, type.MetadataXPathReference, field.JavaName);
				writer.WriteLine ("{0}[Register (\"{1}\"{2})]", indent, field.JavaName, field.AdditionalAttributeString ());
				if (field.IsDeprecated)
					writer.WriteLine ("{0}[Obsolete (\"{1}\")]", indent, field.DeprecatedComment);
				if (field.Annotation != null)
					writer.WriteLine ("{0}{1}", indent, field.Annotation);

				// the Value complication is due to constant enum from negative integer value (C# compiler requires explicit parenthesis).
				writer.WriteLine ("{0}{1} const {2} {3} = ({2}) {4};", indent, field.Visibility, opt.GetOutputName (field.Symbol.FullName), field.Name, field.Value.Contains ('-') && field.Symbol.FullName.Contains ('.') ? '(' + field.Value + ')' : field.Value);
			}
		}

		public void WriteInterface (InterfaceGen @interface, string indent, GenerationInfo gen_info)
		{
			Context.ContextTypes.Push (@interface);

			// interfaces don't nest, so generate as siblings
			foreach (GenBase nest in @interface.NestedTypes) {
				WriteType (nest, indent, gen_info);
				writer.WriteLine ();
			}

			WriteInterfaceImplementedMembersAlternative (@interface, indent);

			// If this interface is just fields and we can't generate any of them
			// then we don't need to write the interface
			if (@interface.IsConstSugar && @interface.GetGeneratableFields (opt).Count () == 0)
				return;

			WriteInterfaceDeclaration (@interface, indent);

			// If this interface is just constant fields we don't need to write all the invoker bits
			if (@interface.IsConstSugar)
				return;

			if (!@interface.AssemblyQualifiedName.Contains ('/'))
				WriteInterfaceExtensionsDeclaration (@interface, indent, null);
			WriteInterfaceInvoker (@interface, indent);
			WriteInterfaceEventHandler (@interface, indent);
			Context.ContextTypes.Pop ();
		}

		// For each interface, generate either an abstract method or an explicit implementation method.
		public void WriteInterfaceAbstractMembers (InterfaceGen @interface, ClassGen gen, string indent)
		{
			foreach (var m in @interface.Methods.Where (m => !m.IsInterfaceDefaultMethod && !m.IsStatic)) {
				bool mapped = false;
				string sig = m.GetSignature ();
				if (Context.ContextGeneratedMethods.Any (_ => _.Name == m.Name && _.JniSignature == m.JniSignature))
					continue;
				for (var cls = gen; cls != null; cls = cls.BaseGen)
					if (cls.ContainsMethod (m, false) || cls != gen && gen.ExplicitlyImplementedInterfaceMethods.Contains (sig)) {
						mapped = true;
						break;
					}
				if (mapped)
					continue;
				if (gen.ExplicitlyImplementedInterfaceMethods.Contains (sig))
					WriteMethodExplicitInterfaceImplementation (m, indent, @interface);
				else
					WriteMethodAbstractDeclaration (m, indent, @interface, gen);
				Context.ContextGeneratedMethods.Add (m);
			}
			foreach (var prop in @interface.Properties.Where (p => !p.Getter.IsInterfaceDefaultMethod && !p.Getter.IsStatic)) {
				if (gen.ContainsProperty (prop.Name, false))
					continue;
				WritePropertyAbstractDeclaration (prop, indent, gen);
			}
		}

		public void WriteInterfaceDeclaration (InterfaceGen @interface, string indent)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (ISymbol isym in @interface.Interfaces) {
				InterfaceGen igen = (isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) as InterfaceGen;
				if (igen.IsConstSugar || igen.RawVisibility != "public")
					continue;
				if (sb.Length > 0)
					sb.Append (", ");
				sb.Append (opt.GetOutputName (isym.FullName));
			}

			writer.WriteLine ("{0}// Metadata.xml XPath interface reference: path=\"{1}\"", indent, @interface.MetadataXPathReference);

			if (@interface.IsDeprecated)
				writer.WriteLine ("{0}[ObsoleteAttribute (@\"{1}\")]", indent, @interface.DeprecatedComment);

			if (!@interface.IsConstSugar)
				writer.WriteLine ("{0}[Register (\"{1}\", \"\", \"{2}\"{3})]", indent, @interface.RawJniName, @interface.Namespace + "." + @interface.FullName.Substring (@interface.Namespace.Length + 1).Replace ('.', '/') + "Invoker", @interface.AdditionalAttributeString ());

			if (@interface.TypeParameters != null && @interface.TypeParameters.Any ())
				writer.WriteLine ("{0}{1}", indent, @interface.TypeParameters.ToGeneratedAttributeString ());
			writer.WriteLine ("{0}{1} partial interface {2}{3} {{", indent, @interface.Visibility, @interface.Name,
				@interface.IsConstSugar ? string.Empty : @interface.Interfaces.Count == 0 || sb.Length == 0 ? " : " + GetAllInterfaceImplements () : " : " + sb.ToString ());

			if (opt.SupportDefaultInterfaceMethods && @interface.HasDefaultMethods)
				WriteClassHandle (@interface, indent + "\t", @interface.Name);

			WriteInterfaceFields (@interface, indent + "\t");
			writer.WriteLine ();
			WriteInterfaceProperties (@interface, indent + "\t");
			WriteInterfaceMethods (@interface, indent + "\t");
			writer.WriteLine (indent + "}");
			writer.WriteLine ();
		}

		public void WriteInterfaceExtensionMethods (InterfaceGen @interface, string indent)
		{
			foreach (Method m in @interface.Methods.Where (m => !m.IsStatic)) {
				WriteMethodExtensionOverload (m, indent, @interface.FullName);
				WriteMethodExtensionAsyncWrapper (m, indent, @interface.FullName);
			}
		}

		public void WriteInterfaceEventArgs (InterfaceGen @interface, Method m, string indent)
		{
			string args_name = @interface.GetArgsName (m);
			if (m.RetVal.IsVoid || m.IsEventHandlerWithHandledProperty) {
				if (!m.IsSimpleEventHandler || m.IsEventHandlerWithHandledProperty) {
					writer.WriteLine ("{0}// event args for {1}.{2}", indent, @interface.JavaName, m.JavaName);
					writer.WriteLine ("{0}public partial class {1} : global::System.EventArgs {{", indent, args_name);
					writer.WriteLine ();
					var signature = m.Parameters.GetSignatureDropSender (opt);
					writer.WriteLine ("{0}\tpublic {1} ({2}{3}{4})", indent, args_name,
							m.IsEventHandlerWithHandledProperty ? "bool handled" : "",
							(m.IsEventHandlerWithHandledProperty && signature.Length != 0) ? ", " : "",
							signature);
					writer.WriteLine ("{0}\t{{", indent);
					if (m.IsEventHandlerWithHandledProperty)
						writer.WriteLine ("{0}\t\tthis.handled = handled;", indent);
					foreach (Parameter p in m.Parameters)
						if (!p.IsSender)
							writer.WriteLine ("{0}\t\tthis.{1} = {1};", indent, opt.GetSafeIdentifier (p.Name));
					writer.WriteLine ("{0}\t}}", indent);
					if (m.IsEventHandlerWithHandledProperty) {
						writer.WriteLine ();
						writer.WriteLine ("{0}\tbool handled;", indent);
						writer.WriteLine ("{0}\tpublic bool Handled {{", indent);
						writer.WriteLine ("{0}\t\tget {{ return handled; }}", indent);
						writer.WriteLine ("{0}\t\tset {{ handled = value; }}", indent);
						writer.WriteLine ("{0}\t}}", indent);
					}
					foreach (Parameter p in m.Parameters) {
						if (p.IsSender)
							continue;
						writer.WriteLine ();
						var safeTypeName = p.Type.StartsWith ("params ", StringComparison.Ordinal) ? p.Type.Substring ("params ".Length) : p.Type;
						writer.WriteLine ("{0}\t{1} {2};", indent, opt.GetOutputName (safeTypeName), opt.GetSafeIdentifier (p.Name));
						// AbsListView.IMultiChoiceModeListener.onItemCheckedStateChanged() hit this strict name check, at parameter "@checked".
						writer.WriteLine ("{0}\tpublic {1} {2} {{", indent, opt.GetOutputName (safeTypeName), p.PropertyName);
						writer.WriteLine ("{0}\t\tget {{ return {1}; }}", indent, opt.GetSafeIdentifier (p.Name));
						writer.WriteLine ("{0}\t}}", indent);
					}
					writer.WriteLine ("{0}}}", indent);
					writer.WriteLine ();
				}
			} else {
				writer.WriteLine ("{0}public delegate {1} {2} ({3});", indent, opt.GetOutputName (m.RetVal.FullName), @interface.GetEventDelegateName (m), m.GetSignature (opt));
				writer.WriteLine ();
			}
		}

		public void WriteInterfaceEventHandler (InterfaceGen @interface, string indent)
		{
			if (!@interface.IsListener)
				return;
			//Method m = Methods [0];
			foreach (var method in @interface.Methods.Where (m => m.EventName != string.Empty))
				WriteInterfaceEventArgs (@interface, method, indent);
			WriteInterfaceEventHandlerImpl (@interface, indent);
		}

		public void WriteInterfaceEventHandlerImpl (InterfaceGen @interface, string indent)
		{
			string jniClass = "mono/" + @interface.RawJniName.Replace ('$', '_') + "Implementor";
			writer.WriteLine ("{0}[global::Android.Runtime.Register (\"{1}\"{2})]", indent, jniClass, @interface.AdditionalAttributeString ());
			writer.WriteLine ("{0}internal sealed partial class {1}Implementor : global::Java.Lang.Object, {1} {{", indent, @interface.Name);
			bool needs_sender = @interface.NeedsSender;
			if (needs_sender) {
				writer.WriteLine ();
				writer.WriteLine ("{0}\tobject sender;", indent);
			}
			writer.WriteLine ();
			writer.WriteLine ("{0}\tpublic {1}Implementor ({2})", indent, @interface.Name, needs_sender ? "object sender" : "");
			writer.WriteLine ("{0}\t\t: base (", indent);
			writer.WriteLine ("{0}\t\t\tglobal::Android.Runtime.JNIEnv.StartCreateInstance (\"{1}\", \"()V\"),", indent, jniClass);
			writer.WriteLine ("{0}\t\t\tJniHandleOwnership.TransferLocalRef)", indent);
			writer.WriteLine ("{0}\t{{", indent);
			writer.WriteLine ("{0}\t\tglobal::Android.Runtime.JNIEnv.FinishCreateInstance ({1}, \"()V\");", indent, @interface.GetObjectHandleProperty ("this"));
			if (needs_sender)
				writer.WriteLine ("{0}\t\tthis.sender = sender;", indent);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ();
			var handlers = new List<string> ();
			foreach (var m in @interface.Methods)
				WriteInterfaceEventHandlerImplContent (@interface, m, indent, needs_sender, jniClass, handlers);
			writer.WriteLine ();
			writer.WriteLine ("{0}\tinternal static bool __IsEmpty ({1}Implementor value)", indent, @interface.Name);
			writer.WriteLine ("{0}\t{{", indent);
			if (!@interface.Methods.Any (m => m.EventName != string.Empty) || handlers.Count == 0)
				writer.WriteLine ("{0}\t\treturn true;", indent);
			else
				writer.WriteLine ("{0}\t\treturn {1};", indent,
						string.Join (" && ", handlers.Select (e => string.Format ("value.{0}Handler == null", e))));
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteInterfaceEventHandlerImplContent (InterfaceGen @interface, Method m, string indent, bool needs_sender, string jniClass, List<string> handlers)
		{
			string methodSpec = @interface.Methods.Count > 1 ? m.AdjustedName : String.Empty;
			handlers.Add (methodSpec);
			string args_name = @interface.GetArgsName (m);
			if (m.EventName != string.Empty) {
				writer.WriteLine ("#pragma warning disable 0649");
				writer.WriteLine ("{0}\tpublic {1} {2}Handler;", indent, @interface.GetEventDelegateName (m), methodSpec);
				writer.WriteLine ("#pragma warning restore 0649");
			}
			writer.WriteLine ();
			writer.WriteLine ("{0}\tpublic {1} {2} ({3})", indent, m.RetVal.FullName, m.Name, m.GetSignature (opt));
			writer.WriteLine ("{0}\t{{", indent);
			if (m.EventName == string.Empty) {
				// generate nothing
			} else if (m.IsVoid) {
				writer.WriteLine ("{0}\t\tvar __h = {1}Handler;", indent, methodSpec);
				writer.WriteLine ("{0}\t\tif (__h != null)", indent);
				writer.WriteLine ("{0}\t\t\t__h ({1}, new {2} ({3}));", indent, needs_sender ? "sender" : m.Parameters.SenderName, args_name, m.Parameters.CallDropSender);
			} else if (m.IsEventHandlerWithHandledProperty) {
				writer.WriteLine ("{0}\t\tvar __h = {1}Handler;", indent, methodSpec);
				writer.WriteLine ("{0}\t\tif (__h == null)", indent);
				writer.WriteLine ("{0}\t\t\treturn {1};", indent, m.RetVal.DefaultValue);
				var call = m.Parameters.CallDropSender;
				writer.WriteLine ("{0}\t\tvar __e = new {1} (true{2}{3});", indent, args_name,
						call.Length != 0 ? ", " : "",
						call);
				writer.WriteLine ("{0}\t\t__h ({1}, __e);", indent, needs_sender ? "sender" : m.Parameters.SenderName);
				writer.WriteLine ("{0}\t\treturn __e.Handled;", indent);
			} else {
				writer.WriteLine ("{0}\t\tvar __h = {1}Handler;", indent, methodSpec);
				writer.WriteLine ("{0}\t\treturn __h != null ? __h ({1}) : default ({2});", indent, m.Parameters.GetCall (opt), opt.GetOutputName (m.RetVal.FullName));
			}
			writer.WriteLine ("{0}\t}}", indent);
		}

		public void WriteInterfaceExtensionsDeclaration (InterfaceGen @interface, string indent, string declaringTypeName)
		{
			if (!@interface.Methods.Any (m => m.CanHaveStringOverload) && !@interface.Methods.Any (m => m.Asyncify))
				return;

			writer.WriteLine ("{0}public static partial class {1}{2}Extensions {{", indent, declaringTypeName, @interface.Name);
			WriteInterfaceExtensionMethods (@interface, indent + "\t");
			writer.WriteLine (indent + "}");
			writer.WriteLine ();
		}

		public void WriteInterfaceFields (InterfaceGen iface, string indent)
		{
			// Interface fields are only supported with DIM
			if (!opt.SupportInterfaceConstants)
				return;

			var seen = new HashSet<string> ();
			var fields = iface.GetGeneratableFields (opt).ToList ();

			WriteFields (fields, indent, iface, seen);
		}

		public void WriteInterfaceImplementedMembersAlternative (InterfaceGen @interface, string indent)
		{
			// Historically .NET has not allowed interface implemented fields or constants, so we
			// initially worked around that by moving them to an abstract class, generally
			// IMyInterface -> MyInterfaceConsts
			// This was later expanded to accomodate static interface methods, creating a more appropriately named class
			// IMyInterface -> MyInterface
			// In this case the XXXConsts class is [Obsolete]'d and simply inherits from the newer class
			// in order to maintain backward compatibility.
			var staticMethods = @interface.Methods.Where (m => m.IsStatic);

			if (@interface.Fields.Any () || staticMethods.Any ()) {
				string name = @interface.HasManagedName
					? @interface.Name.Substring (1) + "Consts"
					: @interface.Name.Substring (1);
				writer.WriteLine ("{0}[Register (\"{1}\"{2}, DoNotGenerateAcw=true)]", indent, @interface.RawJniName, @interface.AdditionalAttributeString ());
				writer.WriteLine ("{0}public abstract class {1} : Java.Lang.Object {{", indent, name);
				writer.WriteLine ();
				writer.WriteLine ("{0}\tinternal {1} ()", indent, name);
				writer.WriteLine ("{0}\t{{", indent);
				writer.WriteLine ("{0}\t}}", indent);

				var seen = new HashSet<string> ();
				bool needsClassRef = WriteFields (@interface.Fields, indent + "\t", @interface, seen) || staticMethods.Any ();
				foreach (var iface in @interface.GetAllImplementedInterfaces ().OfType<InterfaceGen> ()) {
					writer.WriteLine ();
					writer.WriteLine ("{0}\t// The following are fields from: {1}", indent, iface.JavaName);
					bool v = WriteFields (iface.Fields, indent + "\t", iface, seen);
					needsClassRef = needsClassRef || v;
				}

				foreach (var m in @interface.Methods.Where (m => m.IsStatic))
					WriteMethod (m, indent + "\t", @interface, true);

				if (needsClassRef) {
					writer.WriteLine ();
					WriteClassHandle (@interface, indent + "\t", name);
				}

				writer.WriteLine ("{0}}}", indent, @interface.Name);
				writer.WriteLine ();

				if (!@interface.HasManagedName) {
					writer.WriteLine ("{0}[Register (\"{1}\"{2}, DoNotGenerateAcw=true)]", indent, @interface.RawJniName, @interface.AdditionalAttributeString ());
					writer.WriteLine ("{0}[global::System.Obsolete (\"Use the '{1}' type. This type will be removed in a future release.\")]", indent, name);
					writer.WriteLine ("{0}public abstract class {1}Consts : {1} {{", indent, name);
					writer.WriteLine ();
					writer.WriteLine ("{0}\tprivate {1}Consts ()", indent, name);
					writer.WriteLine ("{0}\t{{", indent);
					writer.WriteLine ("{0}\t}}", indent);
					writer.WriteLine ("{0}}}", indent);
					writer.WriteLine ();
				}
			}
		}

		public void WriteInterfaceInvoker (InterfaceGen @interface, string indent)
		{
			writer.WriteLine ("{0}[global::Android.Runtime.Register (\"{1}\", DoNotGenerateAcw=true{2})]", indent, @interface.RawJniName, @interface.AdditionalAttributeString ());
			writer.WriteLine ("{0}internal partial class {1}Invoker : global::Java.Lang.Object, {1} {{", indent, @interface.Name);
			writer.WriteLine ();
			WriteInterfaceInvokerHandle (@interface, indent + "\t", @interface.Name + "Invoker");
			writer.WriteLine ("{0}\t{1}IntPtr class_ref;", indent, opt.BuildingCoreAssembly ? "new " : "");
			writer.WriteLine ();
			writer.WriteLine ("{0}\tpublic static {1} GetObject (IntPtr handle, JniHandleOwnership transfer)", indent, @interface.Name);
			writer.WriteLine ("{0}\t{{", indent);
			writer.WriteLine ("{0}\t\treturn global::Java.Lang.Object.GetObject<{1}> (handle, transfer);", indent, @interface.Name);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}\tstatic IntPtr Validate (IntPtr handle)", indent);
			writer.WriteLine ("{0}\t{{", indent);
			writer.WriteLine ("{0}\t\tif (!JNIEnv.IsInstanceOf (handle, java_class_ref))", indent);
			writer.WriteLine ("{0}\t\t\tthrow new InvalidCastException (string.Format (\"Unable to convert instance of type '{{0}}' to type '{{1}}'.\",", indent);
			writer.WriteLine ("{0}\t\t\t\t\t\tJNIEnv.GetClassNameFromInstance (handle), \"{1}\"));", indent, @interface.JavaName);
			writer.WriteLine ("{0}\t\treturn handle;", indent);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}\tprotected override void Dispose (bool disposing)", indent);
			writer.WriteLine ("{0}\t{{", indent);
			writer.WriteLine ("{0}\t\tif (this.class_ref != IntPtr.Zero)", indent);
			writer.WriteLine ("{0}\t\t\tJNIEnv.DeleteGlobalRef (this.class_ref);", indent);
			writer.WriteLine ("{0}\t\tthis.class_ref = IntPtr.Zero;", indent);
			writer.WriteLine ("{0}\t\tbase.Dispose (disposing);", indent);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}\tpublic {1}Invoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)", indent, @interface.Name);
			writer.WriteLine ("{0}\t{{", indent);
			writer.WriteLine ("{0}\t\tIntPtr local_ref = JNIEnv.GetObjectClass ({1});", indent, Context.ContextType.GetObjectHandleProperty ("this"));
			writer.WriteLine ("{0}\t\tthis.class_ref = JNIEnv.NewGlobalRef (local_ref);", indent);
			writer.WriteLine ("{0}\t\tJNIEnv.DeleteLocalRef (local_ref);", indent);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ();

			HashSet<string> members = new HashSet<string> ();
			WriteInterfacePropertyInvokers (@interface, @interface.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod), indent + "\t", members);
			WriteInterfaceMethodInvokers (@interface, @interface.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod), indent + "\t", members);
			if (@interface.FullName == "Java.Lang.ICharSequence")
				WriteCharSequenceEnumerator (indent + "\t");

			foreach (InterfaceGen iface in @interface.GetAllDerivedInterfaces ()) {
				WriteInterfacePropertyInvokers (@interface, iface.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod), indent + "\t", members);
				WriteInterfaceMethodInvokers (@interface, iface.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod && !@interface.IsCovariantMethod (m) && !(iface.FullName.StartsWith ("Java.Lang.ICharSequence") && m.Name.EndsWith ("Formatted"))), indent + "\t", members);
				if (iface.FullName == "Java.Lang.ICharSequence")
					WriteCharSequenceEnumerator (indent + "\t");
			}
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteInterfaceListenerEvent (InterfaceGen @interface, string indent, string name, string nameSpec, string methodName, string full_delegate_name, bool needs_sender, string wrefSuffix, string add, string remove, bool hasHandlerArgument = false)
		{
			writer.WriteLine ("{0}public event {1} {2} {{", indent, opt.GetOutputName (full_delegate_name), name);
			writer.WriteLine ("{0}\tadd {{", indent);
			writer.WriteLine ("{0}\t\tglobal::Java.Interop.EventHelper.AddEventHandler<{1}, {1}Implementor>(",
					indent, opt.GetOutputName (@interface.FullName));
			writer.WriteLine ("{0}\t\t\t\tref weak_implementor_{1},", indent, wrefSuffix);
			writer.WriteLine ("{0}\t\t\t\t__Create{1}Implementor,", indent, @interface.Name);
			writer.WriteLine ("{0}\t\t\t\t{1},", indent, add + (hasHandlerArgument ? "_Event_With_Handler_Helper" : null));
			writer.WriteLine ("{0}\t\t\t\t__h => __h.{1}Handler += value);", indent, nameSpec);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ("{0}\tremove {{", indent);
			writer.WriteLine ("{0}\t\tglobal::Java.Interop.EventHelper.RemoveEventHandler<{1}, {1}Implementor>(",
					indent, opt.GetOutputName (@interface.FullName));
			writer.WriteLine ("{0}\t\t\t\tref weak_implementor_{1},", indent, wrefSuffix);
			writer.WriteLine ("{0}\t\t\t\t{1}Implementor.__IsEmpty,", indent, opt.GetOutputName (@interface.FullName));
			writer.WriteLine ("{0}\t\t\t\t{1},", indent, remove);
			writer.WriteLine ("{0}\t\t\t\t__h => __h.{1}Handler -= value);", indent, nameSpec);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();

			if (hasHandlerArgument) {
				writer.WriteLine ("{0}void {1} ({2} value)", indent, add + "_Event_With_Handler_Helper", opt.GetOutputName (@interface.FullName));
				writer.WriteLine ("{0}{{", indent);
				writer.WriteLine ("{0}\t{1} (value, null);", indent, add);
				writer.WriteLine ("{0}}}", indent);
				writer.WriteLine ();
			}
		}

		public void WriteInterfaceListenerEventsAndProperties (InterfaceGen @interface, string indent, ClassGen target, string name, string connector_fmt, string add, string remove)
		{
			if (!@interface.IsValid)
				return;
			foreach (var m in @interface.Methods) {
				string nameSpec = @interface.Methods.Count > 1 ? m.EventName ?? m.AdjustedName : String.Empty;
				string nameUnique = String.IsNullOrEmpty (nameSpec) ? name : nameSpec;
				if (nameUnique.StartsWith ("On"))
					nameUnique = nameUnique.Substring (2);
				if (target.ContainsName (nameUnique))
					nameUnique += "Event";
				WriteInterfaceListenerEventOrProperty (@interface, m, indent, target, nameUnique, connector_fmt, add, remove);
			}
		}

		public void WriteInterfaceListenerEventsAndProperties (InterfaceGen @interface, string indent, ClassGen target)
		{
			var methods = target.Methods.Concat (target.Properties.Where (p => p.Setter != null).Select (p => p.Setter));
			var props = new HashSet<string> ();
			var refs = new HashSet<string> ();
			var eventMethods = methods.Where (m => m.IsListenerConnector && m.EventName != String.Empty && m.ListenerType == @interface).OrderBy (m => m.Parameters.Count).GroupBy (m => m.Name).Select (g => g.First ()).Distinct ();
			foreach (var method in eventMethods) {
				string name = method.CalculateEventName (target.ContainsName);
				if (String.IsNullOrEmpty (name)) {
					Report.Warning (0, Report.WarningInterfaceGen + 1, "empty event name in {0}.{1}.", @interface.FullName, method.Name);
					continue;
				}
				if (opt.GetSafeIdentifier (name) != name) {
					Report.Warning (0, Report.WarningInterfaceGen + 4, "event name for {0}.{1} is invalid. `eventName' or `argsType` can be used to assign a valid member name.", @interface.FullName, method.Name);
					continue;
				}
				var prop = target.Properties.FirstOrDefault (p => p.Setter == method);
				if (prop != null) {
					string setter = "__Set" + prop.Name;
					props.Add (prop.Name);
					refs.Add (setter);
					WriteInterfaceListenerEventsAndProperties (@interface, indent, target, name, setter,
						string.Format ("__v => {0} = __v", prop.Name),
						string.Format ("__v => {0} = null", prop.Name));
				} else {
					refs.Add (method.Name);
					string rm = null;
					string remove;
					if (method.Name.StartsWith ("Set"))
						remove = string.Format ("__v => {0} (null)", method.Name);
					else if (method.Name.StartsWith ("Add") &&
						 (rm = "Remove" + method.Name.Substring ("Add".Length)) != null &&
						 methods.Where (m => m.Name == rm).Any ())
						remove = string.Format ("__v => {0} (__v)", rm);
					else
						remove = string.Format ("__v => {{throw new NotSupportedException (\"Cannot unregister from {0}.{1}\");}}",
							@interface.FullName, method.Name);
					WriteInterfaceListenerEventsAndProperties (@interface, indent, target, name, method.Name,
						method.Name,
						remove);
				}
			}

			foreach (var r in refs) {
				writer.WriteLine ("{0}WeakReference weak_implementor_{1};", indent, r);
			}
			writer.WriteLine ();
			writer.WriteLine ("{0}{1}Implementor __Create{2}Implementor ()", indent, opt.GetOutputName (@interface.FullName), @interface.Name);
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn new {1}Implementor ({2});", indent, opt.GetOutputName (@interface.FullName),
				@interface.NeedsSender ? "this" : "");
			writer.WriteLine ("{0}}}", indent);
		}

		public void WriteInterfaceListenerEventOrProperty (InterfaceGen @interface, Method m, string indent, ClassGen target, string name, string connector_fmt, string add, string remove)
		{
			if (m.EventName == string.Empty)
				return;
			string nameSpec = @interface.Methods.Count > 1 ? m.AdjustedName : String.Empty;
			int idx = @interface.FullName.LastIndexOf (".");
			int start = @interface.Name.StartsWith ("IOn") ? 3 : 1;
			string full_delegate_name = @interface.FullName.Substring (0, idx + 1) + @interface.Name.Substring (start, @interface.Name.Length - start - 8) + nameSpec;
			if (m.IsSimpleEventHandler)
				full_delegate_name = "EventHandler";
			else if (m.RetVal.IsVoid || m.IsEventHandlerWithHandledProperty)
				full_delegate_name = "EventHandler<" + @interface.FullName.Substring (0, idx + 1) + @interface.GetArgsName (m) + ">";
			else
				full_delegate_name += "Handler";
			if (m.RetVal.IsVoid || m.IsEventHandlerWithHandledProperty) {
				if (opt.GetSafeIdentifier (name) != name) {
					Report.Warning (0, Report.WarningInterfaceGen + 5, "event name for {0}.{1} is invalid. `eventName' or `argsType` can be used to assign a valid member name.", @interface.FullName, name);
					return;
				} else {
					var mt = target.Methods.Where (method => string.Compare (method.Name, connector_fmt, StringComparison.OrdinalIgnoreCase) == 0 && method.IsListenerConnector).FirstOrDefault ();
					var hasHandlerArgument = mt != null && mt.IsListenerConnector && mt.Parameters.Count == 2 && mt.Parameters [1].Type == "Android.OS.Handler";
					WriteInterfaceListenerEvent (@interface, indent, name, nameSpec, m.AdjustedName, full_delegate_name, !m.Parameters.HasSender, connector_fmt, add, remove, hasHandlerArgument);
				}
			} else {
				if (opt.GetSafeIdentifier (name) != name) {
					Report.Warning (0, Report.WarningInterfaceGen + 6, "event property name for {0}.{1} is invalid. `eventName' or `argsType` can be used to assign a valid member name.", @interface.FullName, name);
					return;
				}
				writer.WriteLine ("{0}WeakReference weak_implementor_{1};", indent, name);
				writer.WriteLine ("{0}{1}Implementor Impl{2} {{", indent, opt.GetOutputName (@interface.FullName), name);
				writer.WriteLine ("{0}\tget {{", indent);
				writer.WriteLine ("{0}\t\tif (weak_implementor_{1} == null || !weak_implementor_{1}.IsAlive)", indent, name);
				writer.WriteLine ("{0}\t\t\treturn null;", indent);
				writer.WriteLine ("{0}\t\treturn weak_implementor_{1}.Target as {2}Implementor;", indent, name, opt.GetOutputName (@interface.FullName));
				writer.WriteLine ("{0}\t}}", indent);
				writer.WriteLine ("{0}\tset {{ weak_implementor_{1} = new WeakReference (value, true); }}", indent, name);
				writer.WriteLine ("{0}}}", indent);
				writer.WriteLine ();
				WriteInterfaceListenerProperty (@interface, indent, name, nameSpec, m.AdjustedName, connector_fmt, full_delegate_name);
			}
		}

		public void WriteInterfaceListenerProperty (InterfaceGen @interface, string indent, string name, string nameSpec, string methodName, string connector_fmt, string full_delegate_name)
		{
			string handlerPrefix = @interface.Methods.Count > 1 ? methodName : string.Empty;
			writer.WriteLine ("{0}public {1} {2} {{", indent, opt.GetOutputName (full_delegate_name), name);
			writer.WriteLine ("{0}\tget {{", indent);
			writer.WriteLine ("{0}\t\t{1}Implementor impl = Impl{2};", indent, opt.GetOutputName (@interface.FullName), name);
			writer.WriteLine ("{0}\t\treturn impl == null ? null : impl.{1}Handler;", indent, handlerPrefix);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ("{0}\tset {{", indent);
			writer.WriteLine ("{0}\t\t{1}Implementor impl = Impl{2};", indent, opt.GetOutputName (@interface.FullName), name);
			writer.WriteLine ("{0}\t\tif (impl == null) {{", indent);
			writer.WriteLine ("{0}\t\t\timpl = new {1}Implementor ({2});", indent, opt.GetOutputName (@interface.FullName), @interface.NeedsSender ? "this" : string.Empty);
			writer.WriteLine ("{0}\t\t\tImpl{1} = impl;", indent, name);
			writer.WriteLine ("{0}\t\t}} else", indent);
			writer.WriteLine ("{0}\t\t\timpl.{1}Handler = value;", indent, nameSpec);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteInterfaceMethodInvokers (InterfaceGen @interface, IEnumerable<Method> methods, string indent, HashSet<string> members)
		{
			foreach (Method m in methods.Where (m => !m.IsStatic)) {
				string sig = m.GetSignature ();
				if (members.Contains (sig))
					continue;
				members.Add (sig);
				WriteMethodInvoker (m, indent, @interface);
			}
		}

		public void WriteInterfaceMethods (InterfaceGen @interface, string indent)
		{
			foreach (var m in @interface.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod)) {
				if (m.Name == @interface.Name || @interface.ContainsProperty (m.Name, true))
					m.Name = "Invoke" + m.Name;

				WriteMethodDeclaration (m, indent, @interface, @interface.AssemblyQualifiedName + "Invoker");
			}

			foreach (var m in @interface.Methods.Where (m => m.IsInterfaceDefaultMethod))
				WriteMethod (m, indent, @interface, true);
		}

		public void WriteInterfaceProperties (InterfaceGen @interface, string indent)
		{
			foreach (var prop in @interface.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod))
				WritePropertyDeclaration (prop, indent, @interface, @interface.AssemblyQualifiedName + "Invoker");

			WriteImplementedProperties (@interface.Properties.Where (p => p.Getter.IsInterfaceDefaultMethod), indent, false, @interface);
		}

		public void WriteInterfacePropertyInvokers (InterfaceGen @interface, IEnumerable<Property> properties, string indent, HashSet<string> members)
		{
			foreach (Property prop in properties) {
				if (members.Contains (prop.Name))
					continue;
				members.Add (prop.Name);
				WritePropertyInvoker (prop, indent, @interface);
			}
		}

		#region "if you're changing this part, also change method in https://github.com/xamarin/xamarin-android/blob/master/src/Mono.Android.Export/CallbackCode.cs"
		public virtual void WriteMethodCallback (Method method, string indent, GenBase type, string property_name, bool as_formatted = false)
		{
			string delegate_type = method.GetDelegateType ();
			writer.WriteLine ("{0}static Delegate {1};", indent, method.EscapedCallbackName);
			writer.WriteLine ("#pragma warning disable 0169");
			if (method.Deprecated != null)
				writer.WriteLine ($"{indent}[Obsolete]");
			writer.WriteLine ("{0}static Delegate {1} ()", indent, method.ConnectorName);
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\tif ({1} == null)", indent, method.EscapedCallbackName);
			writer.WriteLine ("{0}\t\t{1} = JNINativeWrapper.CreateDelegate (({2}) n_{3});", indent, method.EscapedCallbackName, delegate_type, method.Name + method.IDSignature);
			writer.WriteLine ("{0}\treturn {1};", indent, method.EscapedCallbackName);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			if (method.Deprecated != null)
				writer.WriteLine ($"{indent}[Obsolete]");
			writer.WriteLine ("{0}static {1} n_{2} (IntPtr jnienv, IntPtr native__this{3})", indent, method.RetVal.NativeType, method.Name + method.IDSignature, method.Parameters.GetCallbackSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\t{1} __this = global::Java.Lang.Object.GetObject<{1}> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);", indent, opt.GetOutputName (type.FullName));
			foreach (string s in method.Parameters.GetCallbackPrep (opt))
				writer.WriteLine ("{0}\t{1}", indent, s);
			if (String.IsNullOrEmpty (property_name)) {
				string call = "__this." + method.Name + (as_formatted ? "Formatted" : String.Empty) + " (" + method.Parameters.GetCall (opt) + ")";
				if (method.IsVoid)
					writer.WriteLine ("{0}\t{1};", indent, call);
				else
					writer.WriteLine ("{0}\t{1} {2};", indent, method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative (opt, call));
			} else {
				if (method.IsVoid)
					writer.WriteLine ("{0}\t__this.{1} = {2};", indent, property_name, method.Parameters.GetCall (opt));
				else
					writer.WriteLine ("{0}\t{1} {2};", indent, method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative (opt, "__this." + property_name));
			}
			foreach (string cleanup in method.Parameters.GetCallbackCleanup (opt))
				writer.WriteLine ("{0}\t{1}", indent, cleanup);
			if (!method.IsVoid && method.Parameters.HasCleanup)
				writer.WriteLine ("{0}\treturn __ret;", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ("#pragma warning restore 0169");
			writer.WriteLine ();
		}
		#endregion

		public void WriteMethodCustomAttributes (Method method, string indent)
		{
			if (method.GenericArguments != null && method.GenericArguments.Any ())
				writer.WriteLine ("{0}{1}", indent, method.GenericArguments.ToGeneratedAttributeString ());
			if (method.CustomAttributes != null)
				writer.WriteLine ("{0}{1}", indent, method.CustomAttributes);
			if (method.Annotation != null)
				writer.WriteLine ("{0}{1}", indent, method.Annotation);
		}

		public void WriteMethodExplicitInterfaceImplementation (Method method, string indent, GenBase iface)
		{
			//writer.WriteLine ("// explicitly implemented method from " + iface.FullName);
			WriteMethodCustomAttributes (method, indent);
			writer.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (iface.FullName), method.Name, method.GetSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn {1} ({2});", indent, method.Name, method.Parameters.GetCall (opt));
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodExplicitInterfaceInvoker (Method method, string indent, GenBase iface)
		{
			//writer.WriteLine ("\t\t// explicitly implemented invoker method from " + iface.FullName);
			WriteMethodIdField (method, indent);
			writer.WriteLine ("{0}unsafe {1} {2}.{3} ({4})",
				indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (iface.FullName), method.Name, method.GetSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodBody (method, indent + "\t", iface);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodAbstractDeclaration (Method method, string indent, InterfaceGen gen, GenBase impl)
		{
			if (method.RetVal.IsGeneric && gen != null) {
				WriteMethodCustomAttributes (method, indent);
				writer.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (gen.FullName), method.Name, method.GetSignature (opt));
				writer.WriteLine ("{0}{{", indent);
				writer.WriteLine ("{0}\tthrow new NotImplementedException ();", indent);
				writer.WriteLine ("{0}}}", indent);
				writer.WriteLine ();
			} else {
				bool gen_as_formatted = method.IsReturnCharSequence;
				string name = method.AdjustedName;
				WriteMethodCallback (method, indent, impl, null, gen_as_formatted);
				if (method.DeclaringType.IsGeneratable)
					writer.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference (method.DeclaringType));
				writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, method.JavaName, method.JniSignature, method.ConnectorName, method.AdditionalAttributeString ());
				WriteMethodCustomAttributes (method, indent);
				writer.WriteLine ("{0}{1}{2} abstract {3} {4} ({5});",
					indent,
					impl.RequiresNew (method.Name) ? "new " : "",
					method.Visibility,
					opt.GetOutputName (method.RetVal.FullName),
					name,
					method.GetSignature (opt));
				writer.WriteLine ();

				if (gen_as_formatted || method.Parameters.HasCharSequence)
					WriteMethodStringOverload (method, indent);
			}

			WriteMethodAsyncWrapper (method, indent);
		}

		public void WriteMethodDeclaration (Method method, string indent, GenBase type, string adapter)
		{
			if (method.DeclaringType.IsGeneratable)
				writer.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference (method.DeclaringType));
			if (method.Deprecated != null)
				writer.WriteLine ("[Obsolete (@\"{0}\")]", method.Deprecated.Replace ("\"", "\"\""));
			if (method.IsReturnEnumified)
				writer.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			if (method.IsInterfaceDefaultMethod)
				writer.WriteLine ("{0}[global::Java.Interop.JavaInterfaceDefaultMethod]", indent);
			writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})]", indent, method.JavaName, method.JniSignature, method.ConnectorName, method.GetAdapterName (opt, adapter), method.AdditionalAttributeString ());
			WriteMethodCustomAttributes (method, indent);
			writer.WriteLine ("{0}{1} {2} ({3});", indent, opt.GetOutputName (method.RetVal.FullName), method.AdjustedName, method.GetSignature (opt));
			writer.WriteLine ();
		}

		public void WriteMethodEventDelegate (Method method, string indent)
		{
			writer.WriteLine ("{0}public delegate {1} {2}EventHandler ({3});", indent, opt.GetOutputName (method.RetVal.FullName), method.Name, method.GetSignature (opt));
			writer.WriteLine ();
		}

		// This is supposed to generate instantiated generic method output, but I don't think it is done yet.
		public void WriteMethodExplicitIface (Method method, string indent, GenericSymbol gen)
		{
			writer.WriteLine ("{0}// This method is explicitly implemented as a member of an instantiated {1}", indent, gen.FullName);
			WriteMethodCustomAttributes (method, indent);
			writer.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (gen.Gen.FullName), method.Name, method.GetSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			Dictionary<string, string> mappings = new Dictionary<string, string> ();
			for (int i = 0; i < gen.TypeParams.Length; i++)
				mappings [gen.Gen.TypeParameters [i].Name] = gen.TypeParams [i].FullName;
			WriteMethodGenericBody (method, indent + "\t", null, String.Empty, mappings);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		void WriteMethodGenericBody (Method method, string indent, string property_name, string container_prefix, Dictionary<string, string> mappings)
		{
			if (String.IsNullOrEmpty (property_name)) {
				string call = container_prefix + method.Name + " (" + method.Parameters.GetGenericCall (opt, mappings) + ")";
				writer.WriteLine ("{0}{1}{2};", indent, method.IsVoid ? String.Empty : "return ", method.RetVal.GetGenericReturn (opt, call, mappings));
			} else {
				if (method.IsVoid) // setter
					writer.WriteLine ("{0}{1} = {2};", indent, container_prefix + property_name, method.Parameters.GetGenericCall (opt, mappings));
				else // getter
					writer.WriteLine ("{0}return {1};", indent, method.RetVal.GetGenericReturn (opt, container_prefix + property_name, mappings));
			}
		}

		public void WriteMethodIdField (Method method, string indent, bool invoker = false)
		{
			if (invoker) {
				writer.WriteLine ("{0}IntPtr {1};", indent, method.EscapedIdName);
				return;
			}
			WriteMethodIdField (method, indent);
		}

		public void WriteMethodInvoker (Method method, string indent, GenBase type)
		{
			WriteMethodCallback (method, indent, type, null, method.IsReturnCharSequence);
			WriteMethodIdField (method, indent, invoker: true);
			writer.WriteLine ("{0}public unsafe {1}{2} {3} ({4})",
				      indent, method.IsStatic ? "static " : string.Empty, opt.GetOutputName (method.RetVal.FullName), method.AdjustedName, method.GetSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodInvokerBody (method, indent + "\t");
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodInvokerBody (Method method, string indent)
		{
			writer.WriteLine ("{0}if ({1} == IntPtr.Zero)", indent, method.EscapedIdName);
			writer.WriteLine ("{0}\t{1} = JNIEnv.GetMethodID (class_ref, \"{2}\", \"{3}\");", indent, method.EscapedIdName, method.JavaName, method.JniSignature);
			foreach (string prep in method.Parameters.GetCallPrep (opt))
				writer.WriteLine ("{0}{1}", indent, prep);
			WriteParameterListCallArgs (method.Parameters, indent, invoker: true);
			string env_method = "Call" + method.RetVal.CallMethodPrefix + "Method";
			string call = "JNIEnv." + env_method + " (" +
			    Context.ContextType.GetObjectHandleProperty ("this") + ", " + method.EscapedIdName + method.Parameters.GetCallArgs (opt, invoker: true) + ")";
			if (method.IsVoid)
				writer.WriteLine ("{0}{1};", indent, call);
			else
				writer.WriteLine ("{0}{1}{2};", indent, method.Parameters.HasCleanup ? opt.GetOutputName (method.RetVal.FullName) + " __ret = " : "return ", method.RetVal.FromNative (opt, call, true));

			foreach (string cleanup in method.Parameters.GetCallCleanup (opt))
				writer.WriteLine ("{0}{1}", indent, cleanup);

			if (!method.IsVoid && method.Parameters.HasCleanup)
				writer.WriteLine ("{0}return __ret;", indent);
		}

		void WriteMethodStringOverloadBody (Method method, string indent, bool haveSelf)
		{
			var call = new System.Text.StringBuilder ();
			foreach (Parameter p in method.Parameters) {
				string pname = p.Name;
				if (p.Type == "Java.Lang.ICharSequence") {
					pname = p.GetName ("jls_");
					writer.WriteLine ("{0}global::Java.Lang.String {1} = {2} == null ? null : new global::Java.Lang.String ({2});", indent, pname, p.Name);
				} else if (p.Type == "Java.Lang.ICharSequence[]" || p.Type == "params Java.Lang.ICharSequence[]") {
					pname = p.GetName ("jlca_");
					writer.WriteLine ("{0}global::Java.Lang.ICharSequence[] {1} = CharSequence.ArrayFromStringArray({2});", indent, pname, p.Name);
				}
				if (call.Length > 0)
					call.Append (", ");
				call.Append (pname);
			}
			writer.WriteLine ("{0}{1}{2}{3} ({4});", indent, method.RetVal.IsVoid ? String.Empty : opt.GetOutputName (method.RetVal.FullName) + " __result = ", haveSelf ? "self." : "", method.AdjustedName, call.ToString ());
			switch (method.RetVal.FullName) {
				case "void":
					break;
				case "Java.Lang.ICharSequence[]":
					writer.WriteLine ("{0}var __rsval = CharSequence.ArrayToStringArray (__result);", indent);
					break;
				case "Java.Lang.ICharSequence":
					writer.WriteLine ("{0}var __rsval = __result?.ToString ();", indent);
					break;
				default:
					writer.WriteLine ("{0}var __rsval = __result;", indent);
					break;
			}
			foreach (Parameter p in method.Parameters) {
				if (p.Type == "Java.Lang.ICharSequence")
					writer.WriteLine ("{0}{1}?.Dispose ();", indent, p.GetName ("jls_"));
				else if (p.Type == "Java.Lang.ICharSequence[]")
					writer.WriteLine ("{0}if ({1} != null) foreach (global::Java.Lang.String s in {1}) s?.Dispose ();", indent, p.GetName ("jlca_"));
			}
			if (!method.RetVal.IsVoid) {
				writer.WriteLine ($"{indent}return __rsval;");
			}
		}

		void WriteMethodStringOverload (Method method, string indent)
		{
			string static_arg = method.IsStatic ? " static" : String.Empty;
			string ret = opt.GetOutputName (method.RetVal.FullName.Replace ("Java.Lang.ICharSequence", "string"));
			if (method.Deprecated != null)
				writer.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, method.Deprecated.Replace ("\"", "\"\"").Trim ());
			writer.WriteLine ("{0}{1}{2} {3} {4} ({5})", indent, method.Visibility, static_arg, ret, method.Name, method.GetSignature (opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodStringOverloadBody (method, indent + "\t", false);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodExtensionOverload (Method method, string indent, string selfType)
		{
			if (!method.CanHaveStringOverload)
				return;

			string ret = opt.GetOutputName (method.RetVal.FullName.Replace ("Java.Lang.ICharSequence", "string"));
			writer.WriteLine ();

			var parameters = method.GetSignature (opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string");
			writer.WriteLine ("{0}public static {1} {2} (this {3} self{4}{5})", indent, ret, method.Name, selfType, parameters.Length > 0 ? ", " : "", parameters);

			writer.WriteLine ("{0}{{", indent);
			WriteMethodStringOverloadBody (method, indent + "\t", true);
			writer.WriteLine ("{0}}}", indent);
		}

		static string GetDeclaringTypeOfExplicitInterfaceMethod (Method method)
		{
			return method.OverriddenInterfaceMethod != null ?
				     GetDeclaringTypeOfExplicitInterfaceMethod (method.OverriddenInterfaceMethod) :
				     method.DeclaringType.FullName;
		}


		public void WriteMethodAsyncWrapper (Method method, string indent)
		{
			if (!method.Asyncify)
				return;

			string static_arg = method.IsStatic ? " static" : String.Empty;
			string ret;

			if (method.IsVoid)
				ret = "global::System.Threading.Tasks.Task";
			else
				ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName (method.RetVal.FullName) + ">";

			writer.WriteLine ("{0}{1}{2} {3} {4}Async ({5})", indent, method.Visibility, static_arg, ret, method.AdjustedName, method.GetSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn global::System.Threading.Tasks.Task.Run (() => {1} ({2}));", indent, method.AdjustedName, method.Parameters.GetCall (opt));
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodExtensionAsyncWrapper (Method method, string indent, string selfType)
		{
			if (!method.Asyncify)
				return;

			string ret;

			if (method.IsVoid)
				ret = "global::System.Threading.Tasks.Task";
			else
				ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName (method.RetVal.FullName) + ">";

			writer.WriteLine ("{0}public static {1} {2}Async (this {3} self{4}{5})", indent, ret, method.AdjustedName, selfType, method.Parameters.Count > 0 ? ", " : string.Empty, method.GetSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn global::System.Threading.Tasks.Task.Run (() => self.{1} ({2}));", indent, method.AdjustedName, method.Parameters.GetCall (opt));
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethod (Method method, string indent, GenBase type, bool generate_callbacks)
		{
			if (!method.IsValid)
				return;

			bool gen_as_formatted = method.IsReturnCharSequence;
			if (generate_callbacks && method.IsVirtual)
				WriteMethodCallback (method, indent, type, null, gen_as_formatted);

			string name_and_jnisig = method.JavaName + method.JniSignature.Replace ("java/lang/CharSequence", "java/lang/String");
			bool gen_string_overload = !method.IsOverride && method.Parameters.HasCharSequence && !type.ContainsMethod (name_and_jnisig);

			string static_arg = method.IsStatic ? " static" : String.Empty;

			var is_explicit = opt.SupportDefaultInterfaceMethods && type is InterfaceGen && method.OverriddenInterfaceMethod != null;
			var virt_ov = is_explicit ? string.Empty : method.IsOverride ? (opt.SupportDefaultInterfaceMethods && method.OverriddenInterfaceMethod != null ? " virtual" : " override") : method.IsVirtual ? " virtual" : string.Empty;
			string seal = method.IsOverride && method.IsFinal ? " sealed" : null;

			// When using DIM, don't generate "virtual sealed" methods, remove both modifiers instead
			if (opt.SupportDefaultInterfaceMethods && method.OverriddenInterfaceMethod != null && virt_ov == " virtual" && seal == " sealed") {
				virt_ov = string.Empty;
				seal = string.Empty;
			}

			if ((string.IsNullOrEmpty (virt_ov) || virt_ov == " virtual") && type.RequiresNew (method.AdjustedName)) {
				virt_ov = " new" + virt_ov;
			}
			string ret = opt.GetOutputName (method.RetVal.FullName);
			WriteMethodIdField (method, indent);
			if (method.DeclaringType.IsGeneratable)
				writer.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference (method.DeclaringType));
			if (method.Deprecated != null)
				writer.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, method.Deprecated.Replace ("\"", "\"\""));
			if (method.IsReturnEnumified)
				writer.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]",
			    indent, method.JavaName, method.JniSignature, method.IsVirtual ? method.GetConnectorNameFull (opt) : String.Empty, method.AdditionalAttributeString ());
			WriteMethodCustomAttributes (method, indent);

			var visibility = type is InterfaceGen && !method.IsStatic ? string.Empty : method.Visibility;

			writer.WriteLine ("{0}{1}{2}{3}{4} unsafe {5} {6}{7} ({8})",
				indent,
				visibility,
				static_arg,
				virt_ov,
				seal,
				ret,
				is_explicit ? GetDeclaringTypeOfExplicitInterfaceMethod (method.OverriddenInterfaceMethod) + '.' : string.Empty,
				method.AdjustedName,
				method.GetSignature (opt));

			writer.WriteLine ("{0}{{", indent);
			WriteMethodBody (method, indent + "\t", type);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();

			//NOTE: Invokers are the only place false is passed for generate_callbacks, they do not need string overloads
			if (generate_callbacks && (gen_string_overload || gen_as_formatted))
				WriteMethodStringOverload (method, indent);

			WriteMethodAsyncWrapper (method, indent);
		}

		public void WriteParameterListCallArgs (ParameterList parameters, string indent, bool invoker)
		{
			if (parameters.Count == 0)
				return;
			string JValue = "JValue";
			switch (opt.CodeGenerationTarget) {
				case CodeGenerationTarget.XAJavaInterop1:
				case CodeGenerationTarget.JavaInterop1:
					JValue = invoker ? JValue : "JniArgumentValue";
					break;
			}
			writer.WriteLine ("{0}{1}* __args = stackalloc {1} [{2}];", indent, JValue, parameters.Count);
			for (int i = 0; i < parameters.Count; ++i) {
				var p = parameters [i];
				writer.WriteLine ("{0}__args [{1}] = new {2} ({3});", indent, i, JValue, p.GetCall (opt));
			}
		}

		public void WriteProperty (Property property, GenBase gen, string indent, bool with_callbacks = true, bool force_override = false)
		{
			// <TechnicalDebt>
			// This is a special workaround for AdapterView inheritance.
			// (How it is special? They have hand-written bindings AND brings generic
			// version of AdapterView<T> in the inheritance, also added by metadata!)
			//
			// They are on top of fragile hand-bound code, and when we are making changes
			// in generator, they bite. Since we are not going to bring API breakage
			// right now, we need special workarounds to get things working.
			//
			// So far, what we need here is to have AbsSpinner.Adapter compile.
			//
			// > platforms/*/src/generated/Android.Widget.AbsSpinner.cs(156,56): error CS0533:
			// > `Android.Widget.AbsSpinner.Adapter' hides inherited abstract member
			// > `Android.Widget.AdapterView<Android.Widget.ISpinnerAdapter>.Adapter
			//
			// It is because the AdapterView<T>.Adapter is hand-bound and cannot be
			// detected by generator!
			//
			// So, we explicitly treat it as a special-case.
			//
			// Then, Spinner, ListView and GridView instantiate them, so they are also special cases.
			// </TechnicalDebt>
			if (property.Name == "Adapter" &&
			    (property.Getter.DeclaringType.BaseGen.FullName == "Android.Widget.AdapterView" ||
			     property.Getter.DeclaringType.BaseGen.BaseGen != null && property.Getter.DeclaringType.BaseGen.BaseGen.FullName == "Android.Widget.AdapterView"))
				force_override = true;
			// ... and the above breaks generator tests...
			if (property.Name == "Adapter" &&
			    (property.Getter.DeclaringType.BaseGen.FullName == "Xamarin.Test.AdapterView" ||
			     property.Getter.DeclaringType.BaseGen.BaseGen != null && property.Getter.DeclaringType.BaseGen.BaseGen.FullName == "Xamarin.Test.AdapterView"))
				force_override = true;

			string decl_name = property.AdjustedName;
			string needNew = gen.RequiresNew (decl_name) ? " new" : "";
			string virtual_override = String.Empty;
			bool is_virtual = property.Getter.IsVirtual && (property.Setter == null || property.Setter.IsVirtual);
			if (with_callbacks && is_virtual) {
				virtual_override = needNew + " virtual";
				WriteMethodCallback (property.Getter, indent, gen, property.AdjustedName);
			}
			if (with_callbacks && is_virtual && property.Setter != null) {
				virtual_override = needNew + " virtual";
				WriteMethodCallback (property.Setter, indent, gen, property.AdjustedName);
			}
			virtual_override = force_override ? " override" : virtual_override;
			if ((property.Getter ?? property.Setter).IsStatic)
				virtual_override = " static";
			// It should be using AdjustedName instead of Name, but ICharSequence ("Formatted") properties are not caught by this...
			else if (gen.BaseSymbol != null && gen.BaseSymbol.GetPropertyByName (property.Name, true) != null)
				virtual_override = " override";

			WriteMethodIdField (property.Getter, indent);
			if (property.Setter != null)
				WriteMethodIdField (property.Setter, indent);
			string visibility = gen is InterfaceGen ? string.Empty : property.Getter.IsAbstract && property.Getter.RetVal.IsGeneric ? "protected" : (property.Setter ?? property.Getter).Visibility;
			// Unlike [Register], mcs does not allow applying [Obsolete] on property accessors, so we can apply them only under limited condition...
			if (property.Getter.Deprecated != null && (property.Setter == null || property.Setter.Deprecated != null))
				writer.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, property.Getter.Deprecated.Replace ("\"", "\"\"").Trim () + (property.Setter != null && property.Setter.Deprecated != property.Getter.Deprecated ? " " + property.Setter.Deprecated.Replace ("\"", "\"\"").Trim () : null));
			WriteMethodCustomAttributes (property.Getter, indent);
			writer.WriteLine ("{0}{1}{2} unsafe {3} {4} {{", indent, visibility, virtual_override, opt.GetOutputName (property.Getter.ReturnType), decl_name);
			if (gen.IsGeneratable)
				writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, property.Getter.JavaName, property.Getter.Parameters.GetMethodXPathPredicate ());
			writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, property.Getter.JavaName, property.Getter.JniSignature, property.Getter.GetConnectorNameFull (opt), property.Getter.AdditionalAttributeString ());
			writer.WriteLine ("{0}\tget {{", indent);
			WriteMethodBody (property.Getter, indent + "\t\t", gen);
			writer.WriteLine ("{0}\t}}", indent);
			if (property.Setter != null) {
				if (gen.IsGeneratable)
					writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, property.Setter.JavaName, property.Setter.Parameters.GetMethodXPathPredicate ());
				WriteMethodCustomAttributes (property.Setter, indent);
				writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, property.Setter.JavaName, property.Setter.JniSignature, property.Setter.GetConnectorNameFull (opt), property.Setter.AdditionalAttributeString ());
				writer.WriteLine ("{0}\tset {{", indent);
				string pname = property.Setter.Parameters [0].Name;
				property.Setter.Parameters [0].Name = "value";
				WriteMethodBody (property.Setter, indent + "\t\t", gen);
				property.Setter.Parameters [0].Name = pname;
				writer.WriteLine ("{0}\t}}", indent);
			} else if (property.GenerateDispatchingSetter) {
				writer.WriteLine ("{0}// This is a dispatching setter", indent + "\t");
				writer.WriteLine ("{0}set {{ Set{1} (value); }}", indent + "\t", property.Name);
			}
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();

			if (property.Type.StartsWith ("Java.Lang.ICharSequence") && virtual_override != " override")
				WritePropertyStringVariant (property, indent);
		}

		public void WritePropertyAbstractDeclaration (Property property, string indent, GenBase gen)
		{
			bool overrides = false;
			var baseProp = gen.BaseSymbol != null ? gen.BaseSymbol.GetPropertyByName (property.Name, true) : null;
			if (baseProp != null) {
				if (baseProp.Type != property.Getter.Return) {
					// This may not be required if we can change generic parameter support to return constrained type (not just J.L.Object).
					writer.WriteLine ("{0}// skipped generating property {1} because its Java method declaration is variant that we cannot represent in C#", indent, property.Name);
					return;
				}
				overrides = true;
			}

			bool requiresNew = false;
			string abstract_name = property.AdjustedName;
			string visibility = property.Getter.RetVal.IsGeneric ? "protected" : property.Getter.Visibility;
			if (!overrides) {
				requiresNew = gen.RequiresNew (abstract_name);
				WritePropertyCallbacks (property, indent, gen, abstract_name);
			}
			writer.WriteLine ("{0}{1}{2} abstract{3} {4} {5} {{",
					indent,
					visibility,
					requiresNew ? " new" : "",
					overrides ? " override" : "",
					opt.GetOutputName (property.Getter.ReturnType),
					abstract_name);
			if (gen.IsGeneratable)
				writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, property.Getter.JavaName, property.Getter.Parameters.GetMethodXPathPredicate ());
			if (property.Getter.IsReturnEnumified)
				writer.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			WriteMethodCustomAttributes (property.Getter, indent);
			writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})] get;", indent, property.Getter.JavaName, property.Getter.JniSignature, property.Getter.GetConnectorNameFull (opt), property.Getter.AdditionalAttributeString ());
			if (property.Setter != null) {
				if (gen.IsGeneratable)
					writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, property.Setter.JavaName, property.Setter.Parameters.GetMethodXPathPredicate ());
				WriteMethodCustomAttributes (property.Setter, indent);
				writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})] set;", indent, property.Setter.JavaName, property.Setter.JniSignature, property.Setter.GetConnectorNameFull (opt), property.Setter.AdditionalAttributeString ());
			}
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			if (property.Type.StartsWith ("Java.Lang.ICharSequence"))
				WritePropertyStringVariant (property, indent);
		}

		public void WritePropertyCallbacks (Property property, string indent, GenBase gen)
			=> WritePropertyCallbacks (property, indent, gen, property.AdjustedName);

		public void WritePropertyCallbacks (Property property, string indent, GenBase gen, string name)
		{
			WriteMethodCallback (property.Getter, indent, gen, name);

			if (property.Setter != null)
				WriteMethodCallback (property.Setter, indent, gen, name);
		}

		public void WritePropertyExplicitInterface (Property property, string indent, GenericSymbol gen, string adapter)
		{
			Dictionary<string, string> mappings = new Dictionary<string, string> ();
			for (int i = 0; i < gen.TypeParams.Length; i++)
				mappings [gen.Gen.TypeParameters [i].Name] = gen.TypeParams [i].FullName;

			//If the property type is Java.Lang.Object, we don't need to generate an explicit implementation
			if (property.Getter?.RetVal.GetGenericType (mappings) == "Java.Lang.Object")
				return;
			if (property.Setter?.Parameters [0].GetGenericType (mappings) == "Java.Lang.Object")
				return;

			writer.WriteLine ("{0}// This method is explicitly implemented as a member of an instantiated {1}", indent, gen.FullName);
			writer.WriteLine ("{0}{1} {2}.{3} {{", indent, opt.GetOutputName (property.Type), opt.GetOutputName (gen.Gen.FullName), property.AdjustedName);
			if (property.Getter != null) {
				if (gen.Gen.IsGeneratable)
					writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.Gen.MetadataXPathReference, property.Getter.JavaName, property.Getter.Parameters.GetMethodXPathPredicate ());
				if (property.Getter.GenericArguments != null && property.Getter.GenericArguments.Any ())
					writer.WriteLine ("{0}{1}", indent, property.Getter.GenericArguments.ToGeneratedAttributeString ());
				writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] get {{", indent, property.Getter.JavaName, property.Getter.JniSignature, property.Getter.ConnectorName, property.Getter.GetAdapterName (opt, adapter), property.Getter.AdditionalAttributeString ());
				writer.WriteLine ("{0}\t\treturn {1};", indent, property.Name);
				writer.WriteLine ("{0}\t}}", indent);
			}
			if (property.Setter != null) {
				if (gen.Gen.IsGeneratable)
					writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.Gen.MetadataXPathReference, property.Setter.JavaName, property.Setter.Parameters.GetMethodXPathPredicate ());
				if (property.Setter.GenericArguments != null && property.Setter.GenericArguments.Any ())
					writer.WriteLine ("{0}{1}", indent, property.Setter.GenericArguments.ToGeneratedAttributeString ());
				writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] set {{", indent, property.Setter.JavaName, property.Setter.JniSignature, property.Setter.ConnectorName, property.Setter.GetAdapterName (opt, adapter), property.Setter.AdditionalAttributeString ());
				writer.WriteLine ("{0}\t\t{1} = {2};", indent, property.Name, property.Setter.Parameters.GetGenericCall (opt, mappings));
				writer.WriteLine ("{0}\t}}", indent);
			}
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WritePropertyDeclaration (Property property, string indent, GenBase gen, string adapter)
		{
			writer.WriteLine ("{0}{1} {2} {{", indent, opt.GetOutputName (property.Type), property.AdjustedName);
			if (property.Getter != null) {
				if (gen.IsGeneratable)
					writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, property.Getter.JavaName, property.Getter.Parameters.GetMethodXPathPredicate ());
				if (property.Getter.GenericArguments != null && property.Getter.GenericArguments.Any ())
					writer.WriteLine ("{0}{1}", indent, property.Getter.GenericArguments.ToGeneratedAttributeString ());
				writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] get;", indent, property.Getter.JavaName, property.Getter.JniSignature, property.Getter.ConnectorName, property.Getter.GetAdapterName (opt, adapter), property.Getter.AdditionalAttributeString ());
			}
			if (property.Setter != null) {
				if (gen.IsGeneratable)
					writer.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, property.Setter.JavaName, property.Setter.Parameters.GetMethodXPathPredicate ());
				if (property.Setter.GenericArguments != null && property.Setter.GenericArguments.Any ())
					writer.WriteLine ("{0}{1}", indent, property.Setter.GenericArguments.ToGeneratedAttributeString ());
				writer.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] set;", indent, property.Setter.JavaName, property.Setter.JniSignature, property.Setter.ConnectorName, property.Setter.GetAdapterName (opt, adapter), property.Setter.AdditionalAttributeString ());
			}
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WritePropertyInvoker (Property property, string indent, GenBase container)
		{
			WritePropertyCallbacks (property, indent, container);
			WriteMethodIdField (property.Getter, indent, invoker: true);
			if (property.Setter != null)
				WriteMethodIdField (property.Setter, indent, invoker: true);
			writer.WriteLine ("{0}public unsafe {1} {2} {{", indent, opt.GetOutputName (property.Getter.ReturnType), property.AdjustedName);
			writer.WriteLine ("{0}\tget {{", indent);
			WriteMethodInvokerBody (property.Getter, indent + "\t\t");
			writer.WriteLine ("{0}\t}}", indent);
			if (property.Setter != null) {
				string pname = property.Setter.Parameters [0].Name;
				property.Setter.Parameters [0].Name = "value";
				writer.WriteLine ("{0}\tset {{", indent);
				WriteMethodInvokerBody (property.Setter, indent + "\t\t");
				writer.WriteLine ("{0}\t}}", indent);
				property.Setter.Parameters [0].Name = pname;
			}
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WritePropertyStringVariant (Property property, string indent)
		{
			bool is_array = property.Getter.RetVal.IsArray;
			writer.WriteLine ("{0}{1} string{2} {3} {{", indent, (property.Setter ?? property.Getter).Visibility, is_array ? "[]" : String.Empty, property.Name);
			if (is_array)
				writer.WriteLine ("{0}\tget {{ return CharSequence.ArrayToStringArray ({1}); }}", indent, property.AdjustedName);
			else
				writer.WriteLine ("{0}\tget {{ return {1} == null ? null : {1}.ToString (); }}", indent, property.AdjustedName);
			if (property.Setter != null) {
				if (is_array) {
					writer.WriteLine ("{0}\tset {{", indent);
					writer.WriteLine ("{0}\t\tglobal::Java.Lang.ICharSequence[] jlsa = CharSequence.ArrayFromStringArray (value);", indent);
					writer.WriteLine ("{0}\t\t{1} = jlsa;", indent, property.AdjustedName);
					writer.WriteLine ("{0}\t\tforeach (global::Java.Lang.String jls in jlsa) if (jls != null) jls.Dispose ();", indent);
					writer.WriteLine ("{0}\t}}", indent);
				} else {
					writer.WriteLine ("{0}\tset {{", indent);
					writer.WriteLine ("{0}\t\tglobal::Java.Lang.String jls = value == null ? null : new global::Java.Lang.String (value);", indent);
					writer.WriteLine ("{0}\t\t{1} = jls;", indent, property.AdjustedName);
					writer.WriteLine ("{0}\t\tif (jls != null) jls.Dispose ();", indent);
					writer.WriteLine ("{0}\t}}", indent);
				}
			}
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteType (GenBase gen, string indent, GenerationInfo gen_info)
		{
			if (gen is InterfaceGen iface)
				WriteInterface (iface, indent, gen_info);
			else if (gen is ClassGen @class)
				WriteClass (@class, indent, gen_info);
			else
				throw new InvalidOperationException ("Unknown GenBase type");
		}
	}
}

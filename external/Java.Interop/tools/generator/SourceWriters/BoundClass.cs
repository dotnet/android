using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class BoundClass : ClassWriter
	{
		readonly CodeGenerationOptions opt;
		readonly List<TypeWriter> sibling_types = new List<TypeWriter> ();

		public BoundClass (ClassGen klass, CodeGenerationOptions opt, CodeGeneratorContext context, GenerationInfo generationInfo)
		{
			context.ContextTypes.Push (klass);
			context.ContextGeneratedMethods = new List<Method> ();

			generationInfo.TypeRegistrations.Add (new KeyValuePair<string, string> (klass.RawJniName, klass.AssemblyQualifiedName));

			var is_enum = klass.base_symbol != null && klass.base_symbol.FullName == "Java.Lang.Enum";

			if (is_enum)
				generationInfo.Enums.Add (klass.RawJniName.Replace ('/', '.') + ":" + klass.Namespace + ":" + klass.JavaSimpleName);

			this.opt = opt;

			Name = klass.Name;

			SetVisibility (klass.Visibility);
			IsShadow = klass.NeedsNew;
			IsAbstract = klass.IsAbstract;
			IsSealed = klass.IsFinal;
			IsPartial = true;

			UsePriorityOrder = true;

			AddImplementedInterfaces (klass);

			klass.JavadocInfo?.AddJavadocs (Comments);
			Comments.Add ($"// Metadata.xml XPath class reference: path=\"{klass.MetadataXPathReference}\"");

			if (klass.IsDeprecated)
				Attributes.Add (new ObsoleteAttr (klass.DeprecatedComment) { WriteAttributeSuffix = true });

			Attributes.Add (new RegisterAttr (klass.RawJniName, null, null, true, klass.AdditionalAttributeString ()) { UseGlobal = true, UseShortForm = true });

			if (klass.TypeParameters != null && klass.TypeParameters.Any ())
				Attributes.Add (new CustomAttr (klass.TypeParameters.ToGeneratedAttributeString ()));

			// Figure out our base class
			string obj_type = null;

			if (klass.base_symbol != null)
				obj_type = klass.base_symbol is GenericSymbol gs &&
					gs.IsConcrete ? gs.GetGenericType (null) : opt.GetOutputName (klass.base_symbol.FullName);

			if (klass.InheritsObject && obj_type != null)
				Inherits = obj_type;

			// Handle fields
			var seen = new HashSet<string> ();

			SourceWriterExtensions.AddFields (this, klass, klass.Fields, seen, opt, context);

			var ic = new InterfaceConstsClass (klass, seen, opt, context);

			if (ic.ShouldGenerate)
				NestedTypes.Add (ic);

			// Sibling classes
			if (!klass.AssemblyQualifiedName.Contains ('/')) {
				foreach (InterfaceExtensionInfo nestedIface in klass.GetNestedInterfaceTypes ())
					if (nestedIface.Type.Methods.Any (m => m.CanHaveStringOverload) || nestedIface.Type.Methods.Any (m => m.Asyncify))
						sibling_types.Add (new InterfaceExtensionsClass (nestedIface.Type, nestedIface.DeclaringType, opt));
			}

			if (klass.IsAbstract)
				sibling_types.Add (new ClassInvokerClass (klass, opt));

			AddNestedTypes (klass, opt, context, generationInfo);
			AddBindingInfrastructure (klass);
			AddConstructors (klass, opt, context);
			AddProperties (klass, opt);
			AddMethods (klass, opt, context);
			AddAbstractMembers (klass, opt, context);
			AddExplicitGenericInterfaceMembers (klass, opt);
			AddCharSequenceEnumerator (klass);

			context.ContextGeneratedMethods.Clear ();
			context.ContextTypes.Pop ();
		}

		void AddBindingInfrastructure (ClassGen klass)
		{
			// @class.InheritsObject is true unless @class refers to java.lang.Object or java.lang.Throwable. (see ClassGen constructor)
			// If @class's base class is defined in the same api.xml file, then it requires the new keyword to overshadow the internal
			// members of its baseclass since the two classes will be defined in the same assembly. If the base class is not from the
			// same api.xml file, the new keyword is not needed because the internal access modifier already prevents it from being seen.
			var baseFromSameAssembly = klass?.BaseGen?.FromXml ?? false;
			var requireNew = klass.InheritsObject && baseFromSameAssembly;

			Fields.Add (new PeerMembersField (opt, klass.RawJniName, klass.Name, false));
			Properties.Add (new ClassHandleGetter (requireNew));

			if (klass.BaseGen != null && klass.InheritsObject) {
				Properties.Add (new JniPeerMembersGetter ());
				Properties.Add (new ClassThresholdClassGetter ());
				Properties.Add (new ThresholdTypeGetter ());
			}
		}

		void AddConstructors (ClassGen klass, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			// Add required constructor for all JLO inheriting classes
			if (klass.FullName != "Java.Lang.Object" && klass.InheritsObject)
				Constructors.Add (new JavaLangObjectConstructor (klass));

			foreach (var ctor in klass.Ctors) {
				// Don't bind final or protected constructors
				if (klass.IsFinal && ctor.Visibility == "protected")
					continue;

				// Bind Java declared constructor
				Constructors.Add (new BoundConstructor(klass, ctor, klass.InheritsObject, opt, context));

				// If the constructor takes ICharSequence, create an overload constructor that takes a string
				if (ctor.Parameters.HasCharSequence && !klass.ContainsCtor (ctor.JniSignature.Replace ("java/lang/CharSequence", "java/lang/String")))
					Constructors.Add (new StringOverloadConstructor(klass, ctor, klass.InheritsObject, opt, context));
			}
		}

		void AddCharSequenceEnumerator (ClassGen klass)
		{
			if (klass.Interfaces.Any (p => p.FullName == "Java.Lang.ICharSequence")) {
				Methods.Add (new CharSequenceEnumeratorMethod ());
				Methods.Add (new CharSequenceGenericEnumeratorMethod ());
			}
		}

		void AddImplementedInterfaces (ClassGen klass)
		{
			foreach (var isym in klass.Interfaces) {
				if ((!(isym is GenericSymbol gs) ? isym : gs.Gen) is InterfaceGen gen && (gen.IsConstSugar || gen.RawVisibility != "public"))
					continue;

				Implements.Add (opt.GetOutputName (isym.FullName));
			}
		}

		void AddExplicitGenericInterfaceMembers (ClassGen klass, CodeGenerationOptions opt)
		{
			foreach (var gs in klass.Interfaces.Where (sym => sym is GenericSymbol).Cast<GenericSymbol> ().Where (sym => sym.IsConcrete)) {

				// FIXME: not sure if excluding default methods is a valid idea...
				foreach (var m in gs.Gen.Methods) {
					if (m.IsInterfaceDefaultMethod || m.IsStatic)
						continue;
					if (m.IsGeneric)
						Methods.Add (new GenericExplicitInterfaceImplementationMethod (m, gs, opt));
				}

				foreach (var p in gs.Gen.Properties) {
					if (p.Getter?.IsInterfaceDefaultMethod == true || p.Getter?.IsStatic == true)
						continue;

					if (p.Setter?.IsInterfaceDefaultMethod == true || p.Setter?.IsStatic == true)
						continue;

					if (p.IsGeneric) {
						var mappings = new Dictionary<string, string> ();
						for (var i = 0; i < gs.TypeParams.Length; i++)
							mappings [gs.Gen.TypeParameters [i].Name] = gs.TypeParams [i].FullName;

						//If the property type is Java.Lang.Object, we don't need to generate an explicit implementation
						if (p.Getter?.RetVal.GetGenericType (mappings) == "Java.Lang.Object")
							return;
						if (p.Setter?.Parameters [0].GetGenericType (mappings) == "Java.Lang.Object")
							return;

						Properties.Add (new GenericExplicitInterfaceImplementationProperty (p, gs, gs.Gen.AssemblyQualifiedName + "Invoker", mappings, opt));
					}
				}
			}

		}

		void AddAbstractMembers (ClassGen klass, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			if (!klass.IsAbstract)
				return;

			foreach (var gen in klass.GetAllDerivedInterfaces ())
				AddInterfaceAbstractMembers(klass, gen, opt, context);
		}

		// For each interface, generate either an abstract method or an explicit implementation method.
		void AddInterfaceAbstractMembers (ClassGen klass, InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			foreach (var method in iface.Methods.Where (m => !m.IsInterfaceDefaultMethod && !m.IsStatic)) {
				var mapped = false;
				var sig = method.GetSignature ();

				if (context.ContextGeneratedMethods.Any (_ => _.Name == method.Name && _.JniSignature == method.JniSignature))
					continue;

				for (var cls = klass; cls != null; cls = cls.BaseGen)
					if (cls.ContainsMethod (method, false) || cls != klass && klass.ExplicitlyImplementedInterfaceMethods.Contains (sig)) {
						mapped = true;
						break;
					}

				if (mapped)
					continue;

				if (klass.ExplicitlyImplementedInterfaceMethods.Contains (sig))
					Methods.Add (new MethodExplicitInterfaceImplementation(iface, method, opt));
				else
					AddAbstractMethodDeclaration (klass, method, iface);

				context.ContextGeneratedMethods.Add (method);
			}

			foreach (var prop in iface.Properties.Where (p => !p.Getter.IsInterfaceDefaultMethod && !p.Getter.IsStatic)) {
				if (klass.ContainsProperty (prop.Name, false))
					continue;

				AddAbstractPropertyDeclaration (klass, prop, opt);
			}
		}

		void AddMethods (ClassGen klass, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var methodsToDeclare = klass.Methods.AsEnumerable ();

			// This does not exclude overrides (unlike virtual methods) because we're not sure
			// if calling the base interface default method via JNI expectedly dispatches to
			// the derived method.
			var defaultMethods = klass.GetAllDerivedInterfaces ()
				.SelectMany (i => i.Methods)
				.Where (m => m.IsInterfaceDefaultMethod)
				.Where (m => !klass.ContainsMethod (m, false, false));

			var overrides = defaultMethods.Where (m => m.OverriddenInterfaceMethod != null);

			var overridens = defaultMethods.Where (m => overrides.Where (_ => _.Name == m.Name && _.JniSignature == m.JniSignature)
				.Any (mm => mm.DeclaringType.GetAllDerivedInterfaces ().Contains (m.DeclaringType)));

			methodsToDeclare = opt.SupportDefaultInterfaceMethods ? methodsToDeclare : methodsToDeclare.Concat (defaultMethods.Except (overridens)).Where (m => m.DeclaringType.IsGeneratable);

			foreach (var m in methodsToDeclare) {
				var virt = m.IsVirtual;
				m.IsVirtual = !klass.IsFinal && virt;

				if (m.IsAbstract && m.OverriddenInterfaceMethod == null && (opt.SupportDefaultInterfaceMethods || !m.IsInterfaceDefaultMethod))
					AddAbstractMethodDeclaration (klass, m, null);
				else
					AddMethod (klass, m, opt);

				context.ContextGeneratedMethods.Add (m);
				m.IsVirtual = virt;
			}

			var methods = klass.Methods.Concat (klass.Properties.Where (p => p.Setter != null).Select (p => p.Setter));

			foreach (var type in methods.Where (m => m.IsListenerConnector && m.EventName != string.Empty).Select (m => m.ListenerType).Distinct ()) {
				AddInlineComment ($"#region \"Event implementation for {type.FullName}\"");
				SourceWriterExtensions.AddInterfaceListenerEventsAndProperties (this, type, klass, opt);
				AddInlineComment ("#endregion");
			}

		}

		void AddAbstractMethodDeclaration (GenBase klass, Method method, InterfaceGen iface)
		{
			Methods.Add (new BoundMethodAbstractDeclaration (iface, method, opt, klass));

			if (method.IsReturnCharSequence || method.Parameters.HasCharSequence)
				Methods.Add (new BoundMethodStringOverload (method, opt));

			if (method.Asyncify)
				Methods.Add (new MethodAsyncWrapper (method, opt));
		}

		void AddMethod (GenBase klass, Method method, CodeGenerationOptions opt)
		{
			if (!method.IsValid)
				return;

			Methods.Add (new BoundMethod(klass, method, opt, true));

			var name_and_jnisig = method.JavaName + method.JniSignature.Replace ("java/lang/CharSequence", "java/lang/String");
			var gen_string_overload = !method.IsOverride && method.Parameters.HasCharSequence && !klass.ContainsMethod (name_and_jnisig);

			if (gen_string_overload  || method.IsReturnCharSequence)
				Methods.Add (new BoundMethodStringOverload (method, opt));

			if (method.Asyncify)
				Methods.Add (new MethodAsyncWrapper (method, opt));
		}

		void AddProperties (ClassGen klass, CodeGenerationOptions opt)
		{
			foreach (var prop in klass.Properties) {
				var get_virt = prop.Getter.IsVirtual;
				var set_virt = prop.Setter == null ? false : prop.Setter.IsVirtual;

				prop.Getter.IsVirtual = !klass.IsFinal && get_virt;

				if (prop.Setter != null)
					prop.Setter.IsVirtual = !klass.IsFinal && set_virt;

				if (prop.Getter.IsAbstract)
					AddAbstractPropertyDeclaration (klass, prop, opt);
				else
					AddProperty (klass, prop, opt);

				prop.Getter.IsVirtual = get_virt;

				if (prop.Setter != null)
					prop.Setter.IsVirtual = set_virt;
			}
		}

		void AddProperty (ClassGen klass, Property property, CodeGenerationOptions opt)
		{
			var bound_property = new BoundProperty (klass, property, opt, true, false);
			Properties.Add (bound_property);

			if (property.Type.StartsWith ("Java.Lang.ICharSequence") && !bound_property.IsOverride)
				Properties.Add (new BoundPropertyStringVariant (property, opt));
		}

		void AddAbstractPropertyDeclaration (ClassGen klass, Property property, CodeGenerationOptions opt)
		{
			var baseProp = klass.BaseSymbol?.GetPropertyByName (property.Name, true);

			if (baseProp != null) {
				if (baseProp.Type != property.Getter.Return) {
					// This may not be required if we can change generic parameter support to return constrained type (not just J.L.Object).
					AddInlineComment ($"// skipped generating property {property.Name} because its Java method declaration is variant that we cannot represent in C#");
					return;
				}
			}

			Properties.Add (new BoundAbstractProperty (klass, property, opt));

			if (property.Type.StartsWith ("Java.Lang.ICharSequence"))
				Properties.Add (new BoundPropertyStringVariant (property, opt));
		}

		void AddNestedTypes (ClassGen klass, CodeGenerationOptions opt, CodeGeneratorContext context, GenerationInfo genInfo)
		{
			foreach (var nest in klass.NestedTypes) {
				if (klass.BaseGen?.ContainsNestedType (nest) == true && nest is ClassGen c)
					c.NeedsNew = true;

				NestedTypes.Add (SourceWriterExtensions.BuildManagedTypeModel (nest, opt, context, genInfo));
			}
		}

		public override void Write (CodeWriter writer)
		{
			base.Write (writer);

			WriteSiblingClasses (writer);
		}

		public void WriteSiblingClasses (CodeWriter writer)
		{
			foreach (var sibling in sibling_types) {
				writer.WriteLine ();
				sibling.Write (writer);
			}
		}
	}
}

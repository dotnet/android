using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class BoundInterface : InterfaceWriter
	{
		readonly List<TypeWriter> pre_sibling_types = new List<TypeWriter> ();
		readonly List<ISourceWriter> post_sibling_types = new List<ISourceWriter> ();
		readonly bool dont_generate;

		public BoundInterface (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context, GenerationInfo genInfo)
		{
			context.ContextTypes.Push (iface);

			Name = iface.Name;

			AddNestedSiblingTypes (iface, opt, context, genInfo);
			AddAlternativesClass (iface, opt, context);

			// If this interface is just fields and we can't generate any of them
			// then we don't need to write the interface.  We still keep this type
			// because it may have nested types or need an InterfaceMemberAlternativeClass.
			if (iface.IsConstSugar (opt) && iface.GetGeneratableFields (opt).Count () == 0) {
				dont_generate = true;
				return;
			}

			IsPartial = true;

			UsePriorityOrder = true;

			SetVisibility (iface.Visibility);

			iface.JavadocInfo?.AddJavadocs (Comments);
			Comments.Add ($"// Metadata.xml XPath interface reference: path=\"{iface.MetadataXPathReference}\"");

			if (iface.IsDeprecated)
				Attributes.Add (new ObsoleteAttr (iface.DeprecatedComment) { WriteAttributeSuffix = true, WriteEmptyString = true });

			if (!iface.IsConstSugar (opt)) {
				var signature = string.IsNullOrWhiteSpace (iface.Namespace)
					? iface.FullName.Replace ('.', '/')
					: iface.Namespace + "." + iface.FullName.Substring (iface.Namespace.Length + 1).Replace ('.', '/');

				Attributes.Add (new RegisterAttr (iface.RawJniName, string.Empty, signature + "Invoker", additionalProperties: iface.AdditionalAttributeString ()));
			}

			if (iface.TypeParameters != null && iface.TypeParameters.Any ())
				Attributes.Add (new CustomAttr (iface.TypeParameters.ToGeneratedAttributeString ()));

			AddInheritedInterfaces (iface, opt);

			AddClassHandle (iface, opt);
			AddFields (iface, opt, context);
			AddProperties (iface, opt);
			AddMethods (iface, opt);
			AddNestedTypes (iface, opt, context, genInfo);

			// If this interface is just constant fields we don't need to add all the invoker bits
			if (iface.IsConstSugar (opt))
				return;

			if (!iface.AssemblyQualifiedName.Contains ('/')) {
				if (iface.Methods.Any (m => m.CanHaveStringOverload) || iface.Methods.Any (m => m.Asyncify))
					post_sibling_types.Add (new InterfaceExtensionsClass (iface, null, opt));
			}

			post_sibling_types.Add (new InterfaceInvokerClass (iface, opt, context));

			AddInterfaceEventHandler (iface, opt, context);

			context.ContextTypes.Pop ();
		}


		void AddNestedSiblingTypes (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context, GenerationInfo genInfo)
		{
			// Generate sibling types for nested types we don't want to nest
			foreach (var nest in iface.NestedTypes.Where (t => t.Unnest))
				pre_sibling_types.Add (SourceWriterExtensions.BuildManagedTypeModel (nest, opt, context, genInfo));
		}


		void AddAlternativesClass (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			if (iface.NoAlternatives)
				return;

			var staticMethods = iface.Methods.Where (m => m.IsStatic);

			if (iface.Fields.Any () || staticMethods.Any ())
				pre_sibling_types.Add (new InterfaceMemberAlternativeClass (iface, opt, context));
		}

		void AddInterfaceEventHandler (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			if (!iface.IsListener)
				return;

			foreach (var method in iface.Methods.Where (m => m.EventName != string.Empty)) {
				if (method.RetVal.IsVoid || method.IsEventHandlerWithHandledProperty) {
					if (!method.IsSimpleEventHandler || method.IsEventHandlerWithHandledProperty) {
						var event_args_class = post_sibling_types.OfType<InterfaceEventArgsClass> ().SingleOrDefault (c => c.Name == iface.GetArgsName (method));

						// Check if there's an existing EventArgs class to add to
						if (event_args_class is null) {
							event_args_class = new InterfaceEventArgsClass (iface, method);
							post_sibling_types.Add (event_args_class);
						}

						event_args_class.AddMembersFromMethod (iface, method, opt);
					}
				} else {
					var del = new DelegateWriter {
						Name = iface.GetEventDelegateName (method),
						Type = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal)),
						IsPublic = true
					};

					SourceWriterExtensions.AddMethodParameters (del, method.Parameters, opt);

					post_sibling_types.Add (del);
				}
			}

			post_sibling_types.Add (new InterfaceEventHandlerImplClass (iface, opt, context));
		}

		void AddInheritedInterfaces (InterfaceGen iface, CodeGenerationOptions opt)
		{
			foreach (var isym in iface.Interfaces) {
				var igen = (isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) as InterfaceGen;

				// igen *should not* be null here because we *should* only be inheriting interfaces, but
				// in the case of constants on that interface we create a C# *class* that is registered for the
				// Java *interface*. Thus when we do type resolution, we are actually pointed to the
				// Foo abstract class instead of the IFoo interface.
				if (igen is null || igen.IsConstSugar (opt) || igen.RawVisibility != "public")
					continue;

				Implements.Add (opt.GetOutputName (isym.FullName));
			}

			if (Implements.Count == 0 && !iface.IsConstSugar (opt))
				Implements.AddRange (new [] { "IJavaObject", "IJavaPeerable" });
		}

		void AddClassHandle (InterfaceGen iface, CodeGenerationOptions opt)
		{
			if (opt.SupportDefaultInterfaceMethods && (iface.HasDefaultMethods || iface.HasStaticMethods))
				Fields.Add (new PeerMembersField (opt, iface.RawJniName, iface.Name, true));
		}

		void AddFields (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			// Interface fields are only supported with DIM
			if (!opt.SupportInterfaceConstants)
				return;

			var seen = new HashSet<string> ();
			var fields = iface.GetGeneratableFields (opt).ToList ();

			SourceWriterExtensions.AddFields (this, iface, fields, seen, opt, context);
		}

		void AddProperties (InterfaceGen iface, CodeGenerationOptions opt)
		{
			foreach (var prop in iface.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod))
				Properties.Add (new BoundInterfacePropertyDeclaration (iface, prop, iface.AssemblyQualifiedName + "Invoker", opt));

			if (!opt.SupportDefaultInterfaceMethods)
				return;

			var dim_properties = iface.Properties.Where (p => p.Getter.IsInterfaceDefaultMethod || p.Getter.IsStatic);

			foreach (var prop in dim_properties) {
				if (prop.Getter.IsAbstract) {
					var baseProp = iface.BaseSymbol != null ? iface.BaseSymbol.GetPropertyByName (prop.Name, true) : null;
					if (baseProp != null) {
						if (baseProp.Type != prop.Getter.Return) {
							// This may not be required if we can change generic parameter support to return constrained type (not just J.L.Object).
							//writer.WriteLine ("{0}// skipped generating property {1} because its Java method declaration is variant that we cannot represent in C#", indent, property.Name);
							return;
						}
					}

					var bound_property = new BoundAbstractProperty (iface, prop, opt);
					Properties.Add (bound_property);

					if (prop.Type.StartsWith ("Java.Lang.ICharSequence") && !bound_property.IsOverride)
						Properties.Add (new BoundPropertyStringVariant (prop, opt));
				} else {
					var bound_property = new BoundProperty (iface, prop, opt, true, false);
					Properties.Add (bound_property);

					if (prop.Type.StartsWith ("Java.Lang.ICharSequence") && !bound_property.IsOverride)
						Properties.Add (new BoundPropertyStringVariant (prop, opt));
				}
			}
		}

		void AddMethods (InterfaceGen iface, CodeGenerationOptions opt)
		{
			foreach (var m in iface.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod)) {
				if (m.Name == iface.Name || iface.ContainsProperty (m.Name, true))
					m.Name = "Invoke" + m.Name;

				Methods.Add (new BoundInterfaceMethodDeclaration (m, iface.AssemblyQualifiedName + "Invoker", opt));
			}

			if (!opt.SupportDefaultInterfaceMethods)
				return;

			foreach (var method in iface.Methods.Where (m => m.IsInterfaceDefaultMethod || m.IsStatic)) {
				if (!method.IsValid)
					continue;

				Methods.Add (new BoundMethod (iface, method, opt, true));

				var name_and_jnisig = method.JavaName + method.JniSignature.Replace ("java/lang/CharSequence", "java/lang/String");
				var gen_string_overload = !method.IsOverride && method.Parameters.HasCharSequence && !iface.ContainsMethod (name_and_jnisig);

				if (gen_string_overload || method.IsReturnCharSequence)
					Methods.Add (new BoundMethodStringOverload (method, opt));

				if (method.Asyncify)
					Methods.Add (new MethodAsyncWrapper (method, opt));
			}
		}

		void AddNestedTypes (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context, GenerationInfo genInfo)
		{
			// Generate nested types for supported nested types.  This is a new addition in C#8.
			// Prior to this, types nested in an interface had to be generated as sibling types.
			// The "Unnest" property is used to support backwards compatibility with pre-C#8 bindings.
			foreach (var nest in iface.NestedTypes.Where (t => !t.Unnest))
				NestedTypes.Add (SourceWriterExtensions.BuildManagedTypeModel (nest, opt, context, genInfo));
		}

		public override void Write (CodeWriter writer)
		{
			WritePreSiblingClasses (writer);

			if (!dont_generate)
				base.Write (writer);

			WritePostSiblingClasses (writer);
		}

		public void WritePreSiblingClasses (CodeWriter writer)
		{
			foreach (var sibling in pre_sibling_types) {
				sibling.Write (writer);
				writer.WriteLine ();
			}
		}

		public void WritePostSiblingClasses (CodeWriter writer)
		{
			foreach (var sibling in post_sibling_types) {
				writer.WriteLine ();
				sibling.Write (writer);
			}
		}
	}
}

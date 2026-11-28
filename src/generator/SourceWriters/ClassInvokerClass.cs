using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class ClassInvokerClass : ClassWriter
	{
		public ClassInvokerClass (ClassGen klass, CodeGenerationOptions opt)
		{
			Name = $"{klass.Name}Invoker";

			IsInternal = true;
			IsPartial = true;
			UsePriorityOrder = true;

			Inherits = klass.Name;

			foreach (var igen in klass.GetAllDerivedInterfaces ().Where (i => i.IsGeneric))
				Implements.Add (opt.GetOutputName (igen.FullName));

			Attributes.Add (new RegisterAttr (klass.RawJniName, noAcw: true, additionalProperties: klass.AdditionalAttributeString ()) {
				UseGlobal       = true,
			});

			SourceWriterExtensions.AddObsolete (Attributes, klass.DeprecatedComment, opt, klass.IsDeprecated, deprecatedSince: klass.DeprecatedSince);
			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, klass, opt);

			ConstructorWriter ctor = new ConstructorWriter {
				Name        = Name,
				IsPublic    = true,
				BaseCall    = "base (handle, transfer)",
				Parameters  = {
					new MethodParameterWriter ("handle", TypeReferenceWriter.IntPtr),
					new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")),
				},
			};
			if (SourceWriterExtensions.IsThrowable (klass)) {
				ctor.Comments.Add ("/// <safety>");
				ctor.Comments.Add ("/// handle must be zero or a live compatible JNI object. Use an attached thread");
				ctor.Comments.Add ("/// (locals: creating thread), with no concurrent deletion through aliases during construction.");
				ctor.Comments.Add ("/// transfer must match ownership; transferred references cannot be used again,");
				ctor.Comments.Add ("/// even if construction throws.");
				ctor.Comments.Add ("/// </safety>");
				ctor.Comments.Add ("/// <remarks>");
				ctor.Comments.Add ("/// This raw-handle constructor is safe-callable as a compatibility exception.");
				ctor.Comments.Add ("/// The compiler does not enforce these obligations or validate nonzero references.");
				ctor.Comments.Add ("/// </remarks>");
			}

			Constructors.Add (ctor);

			// ClassInvokerHandle
			Fields.Add (new PeerMembersField (opt, klass.RawJniName, $"{klass.Name}Invoker", false));
			Properties.Add (new JniPeerMembersGetter ());

			AddMemberInvokers (klass, klass, opt, new HashSet<string> (), klass.SkippedInvokerMethods);
		}

		void AddMemberInvokers (ClassGen invokerKlass, ClassGen klass, CodeGenerationOptions opt, HashSet<string> members, HashSet<string> skipInvokers)
		{
			AddPropertyInvokers (invokerKlass, klass, klass.Properties, members, opt);
			AddMethodInvokers (invokerKlass, klass, klass.Methods, members, skipInvokers, null, opt);

			foreach (var iface in klass.GetAllDerivedInterfaces ()) {
				AddPropertyInvokers (invokerKlass, klass, iface.Properties.Where (p => !klass.ContainsProperty (p.Name, false, false)), members, opt);
				AddMethodInvokers (invokerKlass, klass, iface.Methods.Where (m => (opt.SupportDefaultInterfaceMethods || !m.IsInterfaceDefaultMethod) && !klass.ContainsMethod (m, false, false) && !klass.IsCovariantMethod (m) && !klass.ExplicitlyImplementedInterfaceMethods.Contains (m.GetSignature ())), members, skipInvokers, iface, opt);
			}

			if (klass.BaseGen != null && klass.BaseGen.FullName != "Java.Lang.Object")
				AddMemberInvokers (invokerKlass, klass.BaseGen, opt, members, skipInvokers);
		}

		void AddPropertyInvokers (ClassGen invokerKlass, ClassGen klass, IEnumerable<Property> properties, HashSet<string> members, CodeGenerationOptions opt)
		{
			var invokerIsObsolete = SourceWriterExtensions.IsTypeObsolete (invokerKlass, opt);

			foreach (var prop in properties) {
				if (members.Contains (prop.Name))
					continue;

				members.Add (prop.Name);

				if ((prop.Getter != null && !prop.Getter.IsAbstract) || (prop.Setter != null && !prop.Setter.IsAbstract))
					continue;

				// BoundAbstractProperty never emits [Obsolete], so omit [Obsolete] on the internal invoker override to avoid CS0809.
				var bound_property = new BoundProperty (klass, prop, opt, false, true, omitObsolete: true, isObsoleteContext: invokerIsObsolete);
				Properties.Add (bound_property);

				if (prop.Type.StartsWith ("Java.Lang.ICharSequence", StringComparison.Ordinal) && !bound_property.IsOverride)
					Properties.Add (new BoundPropertyStringVariant (prop, opt, bound_property));
			}
		}

		void AddMethodInvokers (ClassGen invokerKlass, ClassGen klass, IEnumerable<Method> methods, HashSet<string> members, HashSet<string> skipInvokers, InterfaceGen gen, CodeGenerationOptions opt)
		{
			var invokerIsObsolete = SourceWriterExtensions.IsTypeObsolete (invokerKlass, opt);

			foreach (var m in methods) {
				if (skipInvokers.Contains (m.GetSkipInvokerSignature ()))
					continue;

				var sig = m.GetSignature ();

				if (members.Contains (sig))
					continue;

				members.Add (sig);

				if (!m.IsAbstract)
					continue;

				if (klass.IsExplicitlyImplementedMethod (sig)) {
					Methods.Add (new ExplicitInterfaceInvokerMethod (gen, m, opt, invokerIsObsolete));
				} else {
					m.IsOverride = true;
					Methods.Add (new BoundMethod (klass, m, opt, false, forceOverride: true, isInvoker: true, isObsoleteContext: invokerIsObsolete));

					if (m.Asyncify)
						Methods.Add (new MethodAsyncWrapper (m, opt, isObsoleteContext: invokerIsObsolete));

					m.IsOverride = false;
				}
			}
		}
	}
}

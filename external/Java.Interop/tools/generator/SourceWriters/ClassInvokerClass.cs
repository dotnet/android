using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

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
				MemberType	    = opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1 ? null : (MemberTypes?) MemberTypes.TypeInfo,
			});

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, klass, opt);

			ConstructorWriter ctor = opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1
				? new ConstructorWriter {
					Name        = Name,
					IsPublic    = true,
					BaseCall    = "base (ref reference, options)",
					Parameters  = {
						new MethodParameterWriter ("reference", new TypeReferenceWriter ("ref JniObjectReference")),
						new MethodParameterWriter ("options", new TypeReferenceWriter ("JniObjectReferenceOptions")),
					},
				}
				: new ConstructorWriter {
					Name        = Name,
					IsPublic    = true,
					BaseCall    = "base (handle, transfer)",
					Parameters  = {
						new MethodParameterWriter ("handle", TypeReferenceWriter.IntPtr),
						new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")),
					},
				}
			;

			Constructors.Add (ctor);

			// ClassInvokerHandle
			Fields.Add (new PeerMembersField (opt, klass.RawJniName, $"{klass.Name}Invoker", false));
			Properties.Add (new JniPeerMembersGetter ());
			if (opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1) {
				Properties.Add (new ThresholdTypeGetter ());
			}

			AddMemberInvokers (klass, opt, new HashSet<string> (), klass.SkippedInvokerMethods);
		}

		void AddMemberInvokers (ClassGen klass, CodeGenerationOptions opt, HashSet<string> members, HashSet<string> skipInvokers)
		{
			AddPropertyInvokers (klass, klass.Properties, members, opt);
			AddMethodInvokers (klass, klass.Methods, members, skipInvokers, null, opt);

			foreach (var iface in klass.GetAllDerivedInterfaces ()) {
				AddPropertyInvokers (klass, iface.Properties.Where (p => !klass.ContainsProperty (p.Name, false, false)), members, opt);
				AddMethodInvokers (klass, iface.Methods.Where (m => (opt.SupportDefaultInterfaceMethods || !m.IsInterfaceDefaultMethod) && !klass.ContainsMethod (m, false, false) && !klass.IsCovariantMethod (m) && !klass.ExplicitlyImplementedInterfaceMethods.Contains (m.GetSignature ())), members, skipInvokers, iface, opt);
			}

			if (klass.BaseGen != null && klass.BaseGen.FullName != "Java.Lang.Object")
				AddMemberInvokers (klass.BaseGen, opt, members, skipInvokers);
		}

		void AddPropertyInvokers (ClassGen klass, IEnumerable<Property> properties, HashSet<string> members, CodeGenerationOptions opt)
		{
			foreach (var prop in properties) {
				if (members.Contains (prop.Name))
					continue;

				members.Add (prop.Name);

				if ((prop.Getter != null && !prop.Getter.IsAbstract) || (prop.Setter != null && !prop.Setter.IsAbstract))
					continue;

				var bound_property = new BoundProperty (klass, prop, opt, false, true);
				Properties.Add (bound_property);

				if (prop.Type.StartsWith ("Java.Lang.ICharSequence", StringComparison.Ordinal) && !bound_property.IsOverride)
					Properties.Add (new BoundPropertyStringVariant (prop, opt, bound_property));
			}
		}

		void AddMethodInvokers (ClassGen klass, IEnumerable<Method> methods, HashSet<string> members, HashSet<string> skipInvokers, InterfaceGen gen, CodeGenerationOptions opt)
		{
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
					Methods.Add (new ExplicitInterfaceInvokerMethod (gen, m, opt));
				} else {
					m.IsOverride = true;
					Methods.Add (new BoundMethod (klass, m, opt, false));

					if (m.Asyncify)
						Methods.Add (new MethodAsyncWrapper (m, opt));

					m.IsOverride = false;
				}
			}
		}
	}
}

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
	public class InterfaceInvokerClass : ClassWriter
	{
		public InterfaceInvokerClass (InterfaceGen iface, CodeGenerationOptions opt)
		{
			Name = $"{iface.Name}Invoker";

			IsInternal = true;
			IsPartial = true;
			UsePriorityOrder = true;

			Inherits = "global::Java.Lang.Object";
			Implements.Add (iface.Name);

			bool ji = opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1;

			Attributes.Add (new RegisterAttr (iface.RawJniName, noAcw: true, additionalProperties: iface.AdditionalAttributeString ()) {
				UseGlobal       = true,
				MemberType	    = (!ji) ? null : (MemberTypes?) MemberTypes.TypeInfo,
			});

			SourceWriterExtensions.AddObsolete (Attributes, iface.DeprecatedComment, opt, iface.IsDeprecated, deprecatedSince: iface.DeprecatedSince);

			string members = $"_members_{iface.JavaFullNameId}";

			if (!ji) {
				Properties.Add (new InterfaceHandleGetter (members));
			}

			Properties.Add (new JniPeerMembersGetter (members));

			foreach (var i in GetCompleteImplementedInterfaces (new (), iface).OrderBy (x => x.JavaFullNameId)) {
				var mi = new PeerMembersField (opt, i.RawJniName, $"{iface.Name}Invoker", isInterface:false, name: $"_members_{i.JavaFullNameId}");
				Fields.Add (mi);
			}

			Constructors.Add (new InterfaceInvokerConstructor (opt, iface));

			AddMemberInvokers (iface, new HashSet<string> (), iface.SkippedInvokerMethods, opt);
		}

		static HashSet<InterfaceGen> GetCompleteImplementedInterfaces (HashSet<InterfaceGen> ifaces, InterfaceGen toplevel)
		{
			ifaces.Add (toplevel);
			foreach (var i in toplevel.GetAllImplementedInterfaces ()) {
				GetCompleteImplementedInterfaces (ifaces, i);
			}
			return ifaces;
		}

		void AddMemberInvokers (InterfaceGen iface, HashSet<string> members, HashSet<string> skipInvokers, CodeGenerationOptions opt)
		{
			AddPropertyInvokers (iface, iface.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod), members, opt);
			AddMethodInvokers (iface, iface.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod), members, skipInvokers, opt);
			AddCharSequenceEnumerators (iface);

			foreach (var i in iface.GetAllDerivedInterfaces ()) {
				AddPropertyInvokers (iface, i.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod), members, opt);
				AddMethodInvokers (iface, i.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod && !iface.IsCovariantMethod (m) && !(i.FullName.StartsWith ("Java.Lang.ICharSequence", StringComparison.Ordinal) && m.Name.EndsWith ("Formatted", StringComparison.Ordinal))), members, skipInvokers, opt);
				AddCharSequenceEnumerators (i);
			}
		}

		void AddCharSequenceEnumerators (InterfaceGen iface)
		{
			if (iface.FullName == "Java.Lang.ICharSequence") {
				Methods.Add (new CharSequenceEnumeratorMethod ());
				Methods.Add (new CharSequenceGenericEnumeratorMethod ());
			}
		}

		void AddPropertyInvokers (InterfaceGen iface, IEnumerable<Property> properties, HashSet<string> members, CodeGenerationOptions opt)
		{
			foreach (var prop in properties) {
				if (members.Contains (prop.Name))
					continue;

				members.Add (prop.Name);

				Properties.Add (new InterfaceInvokerProperty (iface, prop, opt));
			}
		}
		
		void AddMethodInvokers (InterfaceGen iface, IEnumerable<Method> methods, HashSet<string> members, HashSet<string> skipInvokers, CodeGenerationOptions opt)
		{
			foreach (var m in methods) {
				if (skipInvokers.Contains (m.GetSkipInvokerSignature ()))
					continue;

				var sig = m.GetSignature ();

				if (members.Contains (sig))
					continue;

				members.Add (sig);

				Methods.Add (new InterfaceInvokerMethod (iface, m, opt));
			}
		}
	}

	public class InterfaceInvokerConstructor : ConstructorWriter
	{
		public InterfaceInvokerConstructor (CodeGenerationOptions opt, InterfaceGen iface)
		{
			Name = iface.Name + "Invoker";

			IsPublic = true;

			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				Parameters.Add (new MethodParameterWriter ("reference", new TypeReferenceWriter ("ref JniObjectReference")));
				Parameters.Add (new MethodParameterWriter ("options", new TypeReferenceWriter ("JniObjectReferenceOptions")));
				BaseCall = "base (ref reference, options)";

			} else {
				Parameters.Add (new MethodParameterWriter ("handle", TypeReferenceWriter.IntPtr));
				Parameters.Add (new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")));
				BaseCall = "base (handle, transfer)";
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceInvokerClass : ClassWriter
	{
		public InterfaceInvokerClass (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			Name = $"{iface.Name}Invoker";

			IsInternal = true;
			IsPartial = true;
			UsePriorityOrder = true;

			Inherits = "global::Java.Lang.Object";
			Implements.Add (iface.Name);

			Attributes.Add (new RegisterAttr (iface.RawJniName, noAcw: true, additionalProperties: iface.AdditionalAttributeString ()) { UseGlobal = true });

			Fields.Add (new PeerMembersField (opt, iface.RawJniName, $"{iface.Name}Invoker", false));

			Properties.Add (new InterfaceHandleGetter ());
			Properties.Add (new JniPeerMembersGetter ());
			Properties.Add (new InterfaceThresholdClassGetter ());
			Properties.Add (new ThresholdTypeGetter ());

			Fields.Add (new FieldWriter { Name = "class_ref", Type = TypeReferenceWriter.IntPtr, IsShadow = opt.BuildingCoreAssembly });

			Methods.Add (new GetObjectMethod (iface, opt));
			Methods.Add (new ValidateMethod (iface));
			Methods.Add (new DisposeMethod ());

			Constructors.Add (new InterfaceInvokerConstructor (iface, context));

			AddMemberInvokers (iface, new HashSet<string> (), opt, context);
		}

		void AddMemberInvokers (InterfaceGen iface, HashSet<string> members, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			AddPropertyInvokers (iface, iface.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod), members, opt, context);
			AddMethodInvokers (iface, iface.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod), members, opt, context);
			AddCharSequenceEnumerators (iface);

			foreach (var i in iface.GetAllDerivedInterfaces ()) {
				AddPropertyInvokers (iface, i.Properties.Where (p => !p.Getter.IsStatic && !p.Getter.IsInterfaceDefaultMethod), members, opt, context);
				AddMethodInvokers (iface, i.Methods.Where (m => !m.IsStatic && !m.IsInterfaceDefaultMethod && !iface.IsCovariantMethod (m) && !(i.FullName.StartsWith ("Java.Lang.ICharSequence", StringComparison.Ordinal) && m.Name.EndsWith ("Formatted", StringComparison.Ordinal))), members, opt, context);
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

		void AddPropertyInvokers (InterfaceGen iface, IEnumerable<Property> properties, HashSet<string> members, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			foreach (var prop in properties) {
				if (members.Contains (prop.Name))
					continue;

				members.Add (prop.Name);

				Properties.Add (new InterfaceInvokerProperty (iface, prop, opt, context));
			}
		}

		void AddMethodInvokers (InterfaceGen iface, IEnumerable<Method> methods, HashSet<string> members, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			foreach (var m in methods) {
				var sig = m.GetSignature ();

				if (members.Contains (sig))
					continue;

				members.Add (sig);

				Methods.Add (new InterfaceInvokerMethod (iface, m, opt, context));
			}
		}
	}

	public class GetObjectMethod : MethodWriter
	{
		// public static IInterface? GetObject (IntPtr handle, JniHandleOwnership transfer)
		// {
		//     return global::Java.Lang.Object.GetObject<IInterface> (handle, transfer);
		// }
		public GetObjectMethod (InterfaceGen iface, CodeGenerationOptions opt)
		{
			Name = "GetObject";

			ReturnType = new TypeReferenceWriter (iface.Name) { Nullable = opt.SupportNullableReferenceTypes };

			IsPublic = true;
			IsStatic = true;

			Parameters.Add (new MethodParameterWriter ("handle", TypeReferenceWriter.IntPtr));
			Parameters.Add (new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")));

			Body.Add ($"return global::Java.Lang.Object.GetObject<{iface.Name}> (handle, transfer);");
		}
	}

	public class ValidateMethod : MethodWriter
	{
		// static IntPtr Validate (IntPtr handle)
		// {
		//     if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
		//         throw new InvalidCastException (string.Format (\"Unable to convert instance of type '{{0}}' to type '{{1}}'.\", JNIEnv.GetClassNameFromInstance (handle), \"{iface.JavaName}\"));
		//
		//     return handle;
		// }
		public ValidateMethod (InterfaceGen iface)
		{
			Name = "Validate";

			ReturnType = TypeReferenceWriter.IntPtr;

			IsStatic = true;

			Parameters.Add (new MethodParameterWriter ("handle", TypeReferenceWriter.IntPtr));

			Body.Add ("if (!JNIEnv.IsInstanceOf (handle, java_class_ref))");
			Body.Add ($"\tthrow new InvalidCastException ($\"Unable to convert instance of type '{{JNIEnv.GetClassNameFromInstance (handle)}}' to type '{iface.JavaName}'.\");");
			Body.Add ("return handle;");
		}
	}

	public class DisposeMethod : MethodWriter
	{
		// protected override void Dispose (bool disposing)
		// {
		//     if (this.class_ref != IntPtr.Zero)
		//         JNIEnv.DeleteGlobalRef (this.class_ref);
		//     this.class_ref = IntPtr.Zero;
		//     base.Dispose (disposing);
		// }
		public DisposeMethod ()
		{
			Name = "Dispose";

			IsProtected = true;
			IsOverride = true;

			Parameters.Add (new MethodParameterWriter ("disposing", TypeReferenceWriter.Bool));
			ReturnType = TypeReferenceWriter.Void;

			Body.Add ("if (this.class_ref != IntPtr.Zero)");
			Body.Add ("\tJNIEnv.DeleteGlobalRef (this.class_ref);");
			Body.Add ("this.class_ref = IntPtr.Zero;");
			Body.Add ("base.Dispose (disposing);");
		}
	}

	public class InterfaceInvokerConstructor : ConstructorWriter
	{
		// public IfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		// {
		//     IntPtr local_ref = JNIEnv.GetObjectClass (this)
		//     this.class_ref = JNIEnv.NewGlobalRef (local_ref);
		//     JNIEnv.DeleteLocalRef (local_ref);
		// }
		public InterfaceInvokerConstructor (InterfaceGen iface, CodeGeneratorContext context)
		{
			Name = iface.Name + "Invoker";

			IsPublic = true;

			Parameters.Add (new MethodParameterWriter ("handle", TypeReferenceWriter.IntPtr));
			Parameters.Add (new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")));

			BaseCall = "base (Validate (handle), transfer)";

			Body.Add ($"IntPtr local_ref = JNIEnv.GetObjectClass ({context.ContextType.GetObjectHandleProperty ("this")});");
			Body.Add ("this.class_ref = JNIEnv.NewGlobalRef (local_ref);");
			Body.Add ("JNIEnv.DeleteLocalRef (local_ref);");
		}
	}
}

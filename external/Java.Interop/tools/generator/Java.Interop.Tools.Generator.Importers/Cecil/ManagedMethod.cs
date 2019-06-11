using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using System;
using System.Linq;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	public class ManagedMethod : Method {
		MethodDefinition m;
		string java_name;
		string java_return;
		bool is_acw;
		bool is_interface_default_method;

		public ManagedMethod (GenBase declaringType, MethodDefinition m)
			: base (declaringType)
		{
			this.m = m;
			GenericArguments = m.GenericArguments ();
			var regatt = m.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			is_interface_default_method = m.CustomAttributes
				.Any (ca => ca.AttributeType.FullName == "Java.Interop.JavaInterfaceDefaultMethodAttribute");
			java_name = regatt != null ? ((string) regatt.ConstructorArguments [0].Value) : m.Name;
			
			foreach (var p in m.GetParameters (regatt))
				Parameters.Add (p);
			
			if (regatt != null) {
				var jnisig = (string)(regatt.ConstructorArguments.Count > 1 ? regatt.ConstructorArguments [1].Value : regatt.Properties.First (p => p.Name == "JniSignature").Argument.Value);
				var rt = JavaNativeTypeManager.ReturnTypeFromSignature (jnisig);
				if (rt != null)
					java_return = rt.Type;
			}
			FillReturnType ();
		}

		public override string AssemblyName => m.DeclaringType.Module.Assembly.FullName;

		public override string Deprecated => m.Deprecated ();

		public override string Visibility => m.Visibility ();

		public override bool IsAcw {
			get { return is_acw; }
		}

		public override bool IsInterfaceDefaultMethod {
			get { return is_interface_default_method; }
		}

		// Strip "Formatted" from ICharSequence-based method. Use this wherever m.Name was used.
		string NameBase {
			get { return IsReturnCharSequence ? m.Name.Substring (0, m.Name.Length - "Formatted".Length) : m.Name; }
		}

		public override string Name {
			get { return m.IsGetter ? (m.Name.StartsWith ("get_Is") && m.Name.Length > 6 && char.IsUpper (m.Name [6]) ? string.Empty : "Get") + NameBase.Substring (4) : m.IsSetter ? (m.Name.StartsWith ("set_Is") && m.Name.Length > 6 && char.IsUpper (m.Name [6])  ? string.Empty : "Set") + NameBase.Substring (4) : NameBase; }
			set { throw new NotImplementedException (); }
		}

		public override string ArgsType {
			get { throw new NotImplementedException (); }
		}

		public override string EventName {
			get { throw new NotImplementedException (); }
		}

		public override string JavaName {
			get { return java_name; }
		}

		public override bool IsAbstract {
			get { return m.IsAbstract; }
		}

		public override bool IsFinal {
			get { return m.IsFinal; }
		}

		public override bool IsStatic {
			get { return m.IsStatic; }
		}

		public override bool IsVirtual {
			get { return m.IsVirtual; }
			set { throw new NotImplementedException (); }
		}

		public override string ManagedReturn {
			get { return m.ReturnType.FullNameCorrected (); }
		}
		
		public override sealed bool IsReturnEnumified {
			get { return m.MethodReturnType.CustomAttributes.Any (c => c.AttributeType.FullName == "Android.Runtime.GeneratedEnumAttribute"); }
		}

		public override string Return {
			get { return java_return ?? m.ReturnType.FullNameCorrected (); }
		}

		protected override string PropertyNameOverride {
			get { return null; }
		}

		public override int SourceApiLevel {
			get { return 0; }
		}

		public override bool Asyncify {
			get { return false; }
		}

		public override string CustomAttributes {
			get { return null; }
		}
	}
}
#endif

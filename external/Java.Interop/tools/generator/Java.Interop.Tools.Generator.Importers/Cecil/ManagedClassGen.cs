using System;
using System.Linq;
using Mono.Cecil;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	public class ManagedClassGen : ClassGen
	{
		TypeDefinition t;
		TypeReference nominal_base_type;

		public ManagedClassGen (TypeDefinition t, CodeGenerationOptions opt)
			: base (new ManagedGenBaseSupport (t, opt))
		{
			this.t = t;
			foreach (var ifaceImpl in t.Interfaces) {
				var iface = ifaceImpl.InterfaceType;
				var def = ifaceImpl.InterfaceType.Resolve ();
				if (def != null && def.IsNotPublic)
					continue;
				AddInterface (iface.FullNameCorrected ());
			}
			bool implements_charsequence = t.Interfaces.Any (it => it.InterfaceType.FullName == "Java.Lang.CharSequence");
			foreach (var m in t.Methods) {
				if (m.IsPrivate || m.IsAssembly || !m.CustomAttributes.Any (ca => ca.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute"))
					continue;
				if (implements_charsequence && t.Methods.Any (mm => mm.Name == m.Name + "Formatted"))
					continue;
				if (m.IsConstructor)
					Ctors.Add (new ManagedCtor (this, m));
				else
					AddMethod (new ManagedMethod (this, m));
			}
			foreach (var f in t.Fields)
				if (!f.IsPrivate && !f.CustomAttributes.Any (ca => ca.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute"))
					AddField (new ManagedField (f));
			for (nominal_base_type = t.BaseType; nominal_base_type != null && (nominal_base_type.HasGenericParameters || nominal_base_type.IsGenericInstance); nominal_base_type = nominal_base_type.Resolve ().BaseType)
				; // iterate up to non-generic type, at worst System.Object.
		}

		public override string BaseType {
			get { return nominal_base_type != null ? nominal_base_type.FullNameCorrected () : null; }
			set { throw new NotSupportedException (); }
		}

		public override bool IsAbstract {
			get { return t.IsAbstract; }
		}

		public override bool IsFinal {
			get { return t.IsSealed; }
		}
	}
}
#endif

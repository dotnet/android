using System;
using System.Linq;
using Mono.Cecil;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	public class ManagedInterfaceGen : InterfaceGen {
		public ManagedInterfaceGen (TypeDefinition t, CodeGenerationOptions opt)
			: base (new ManagedGenBaseSupport (t, opt))
		{
			foreach (var ifaceImpl in t.Interfaces) {
				AddInterface (ifaceImpl.InterfaceType.FullNameCorrected ());
			}
			foreach (var m in t.Methods) {
				if (m.IsPrivate || m.IsAssembly || !m.CustomAttributes.Any (ca => ca.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute"))
					continue;
				AddMethod (new ManagedMethod (this, m));
			}
		}

		public override string ArgsType {
			get { throw new NotImplementedException (); }
		}

		public override bool MayHaveManagedGenericArguments {
			get { return !this.IsAcw; }
		}
	}
}
#endif

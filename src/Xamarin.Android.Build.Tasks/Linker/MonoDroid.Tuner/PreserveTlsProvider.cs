using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Tuner;

namespace MonoDroid.Tuner
{
	public class PreserveTlsProvider : BaseSubStep
	{
		public string TlsProvider { get; set; }

		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			return TlsProvider != null && assembly.Name.Name == "System";
		}

		public override SubStepTargets Targets {
			get { return SubStepTargets.Method; }
		}

		public override void ProcessMethod (MethodDefinition method)
		{
			if (method.Name == "CreateDefaultProviderImpl" && method.DeclaringType.FullName == "Mono.Net.Security.MonoTlsProviderFactory")
				ProcessCreateProviderImpl (method);
		}

		TypeDefinition GetTlsProvider (ModuleDefinition module)
		{
			string provider;
			if (string.IsNullOrEmpty (TlsProvider))
				provider = "default";
			else
				provider = TlsProvider;

			TypeDefinition type;
			switch (provider) {
			case "btls":
				type = module.GetType ("Mono.Btls.MonoBtlsProvider");
				break;
			case "legacy":
			case "default":
				type = module.GetType ("Mono.Net.Security.LegacyTlsProvider");
				break;
			default:
				throw new InvalidOperationException (string.Format ("Unknown TlsProvider `{0}`.", provider));
			}
			if (type == null)
				throw new InvalidOperationException (string.Format ("Cannot load TlsProvider `{0}`.", provider));
			return type;
		}

		MethodDefinition FindDefaultCtor (TypeDefinition type)
		{
			foreach (var m in type.Methods) {
				if (m.IsStatic || !m.IsConstructor || m.HasParameters)
					continue;
				return m;
			}
			return null;
		}

		MethodReference FindProviderConstructor (ModuleDefinition module)
		{
			var providerType = GetTlsProvider (module);
			if (providerType == null)
				return null;

			var ctor = FindDefaultCtor (providerType);
			if (ctor == null)
				throw new InvalidOperationException ();

			return module.ImportReference (ctor);
		}

		void ProcessCreateProviderImpl (MethodDefinition method)
		{
			var providerCtor = FindProviderConstructor (method.Module);
			if (providerCtor == null)
				return;

			// re-write MonoTlsProviderFactory.CreateDefaultProviderImpl()
			var body = new MethodBody (method);
			var il = body.GetILProcessor ();
			if (providerCtor != null)
				il.Emit (OpCodes.Newobj, providerCtor);
			else
				il.Emit (OpCodes.Ldnull);
			il.Emit (OpCodes.Ret);
			method.Body = body;
		}
	}
}


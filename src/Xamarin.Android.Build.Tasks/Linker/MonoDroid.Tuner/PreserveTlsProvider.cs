using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Tuner;

namespace MonoDroid.Tuner
{
	public class PreserveTlsProvider : BaseSubStep
	{
		public string TlsProviderType { get; set; }

		public override bool IsActiveFor (Mono.Cecil.AssemblyDefinition assembly)
		{
			return TlsProviderType != null && (assembly.Name.Name == "System" || assembly.Name.Name == "Mono.Android");
		}

		public override SubStepTargets Targets {
			get { return SubStepTargets.Method; }
		}

		public override void ProcessMethod (MethodDefinition method)
		{
			if (method.Name == "CreateDefaultProviderImpl" && method.DeclaringType.FullName == "Mono.Net.Security.MonoTlsProviderFactory")
				ProcessCreateProviderImpl (method);
		}

		protected AssemblyDefinition GetAssembly (string assemblyName)
		{
			AssemblyDefinition ad;
			context.TryGetLinkedAssembly (assemblyName, out ad);
			return ad;
		}

		protected TypeDefinition GetType (AssemblyDefinition assembly, string typeName)
		{
			return assembly.MainModule.GetType (typeName);
		}

		protected TypeDefinition GetType (string assemblyName, string typeName)
		{
			AssemblyDefinition ad = GetAssembly (assemblyName);
			return ad == null ? null : GetType (ad, typeName);
		}

		bool MarkType (string assemblyName, string typeName)
		{
			var type = GetType (assemblyName, typeName);
			if (type != null) {
				context.Annotations.Mark (type);
				context.Annotations.SetPreserve (type, Mono.Linker.TypePreserve.All);
				return true;
			}
			return false;
		}

		string GetAssemblyNameFromTypeName (string typeName, out string simpleTypeName)
		{
			simpleTypeName = null;
			var parts = typeName.Split (new char [] { ',' }, 2);
			if (parts.Length != 2)
				return null;

			var anr = AssemblyNameReference.Parse (parts [1].Trim ());
			if (anr == null)
				return null;

			simpleTypeName = parts [0].Trim ();
			return anr.Name;
		}

		TypeDefinition GetTlsProvider (ModuleDefinition module)
		{
			string provider;
			if (string.IsNullOrEmpty (TlsProviderType))
				provider = "default";
			else
				provider = TlsProviderType;

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


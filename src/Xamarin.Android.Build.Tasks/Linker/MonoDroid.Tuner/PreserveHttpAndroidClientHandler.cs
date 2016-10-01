using System;
using Mono.Cecil;
using Mono.Tuner;

namespace MonoDroid.Tuner
{
	public class PreserveHttpAndroidClientHandler : BaseSubStep
	{
		public string HttpClientHandlerType { get; set; }

		public override bool IsActiveFor (Mono.Cecil.AssemblyDefinition assembly)
		{
			return HttpClientHandlerType != null && (assembly.Name.Name == "System.Net.Http" || assembly.Name.Name == "Mono.Android");
		}

		public override SubStepTargets Targets {
			get { return SubStepTargets.Method; }
		}

		public override void ProcessMethod (MethodDefinition method)
		{
			if (method.Name == "GetDefaultHandler" && method.DeclaringType.FullName == "System.Net.Http.HttpClient")
				Mark ();
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

		void Mark ()
		{
			var androidEnvironmentType = GetType ("Mono.Android", "Android.Runtime.AndroidEnvironment");
			if (androidEnvironmentType != null) {
				foreach (var method in androidEnvironmentType.Methods) {
					if (method.Name == "GetHttpMessageHandler") {
						context.Annotations.AddPreservedMethod (androidEnvironmentType, method);
						break;
					}
				}
			}

			if (MarkType ("Mono.Android", HttpClientHandlerType))
				return;

			string simpleTypeName;
			var assemblyName = GetAssemblyNameFromTypeName (HttpClientHandlerType, out simpleTypeName);
			if (assemblyName != null && MarkType (assemblyName, simpleTypeName))
				return;

			foreach (var assembly in context.GetAssemblies ()) {
				var clientTypeRef = assembly.MainModule.GetType (HttpClientHandlerType, true);
				if (clientTypeRef == null)
					continue;

				var clientTypeDef = clientTypeRef.Resolve ();
				if (clientTypeDef == null)
					continue;

				context.Annotations.Mark (clientTypeDef);
				context.Annotations.SetPreserve (clientTypeDef, Mono.Linker.TypePreserve.All);
				break;
			}
		}
	}
}


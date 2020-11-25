using Mono.Cecil;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	class PreserveJavaInterfaces : BaseSubStep
	{
		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			return assembly.Name.Name == "Mono.Android" || assembly.MainModule.HasTypeReference ("Android.Runtime.IJavaObject");
		}

		public override SubStepTargets Targets { get { return SubStepTargets.Type;  } }

		public override void ProcessType (TypeDefinition type)
		{
			// If we are preserving a Mono.Android interface,
			// preserve all members on the interface.
			if (!type.IsInterface)
				return;

			// Mono.Android interfaces will always inherit IJavaObject
			if (!type.ImplementsIJavaObject ())
				return;

			foreach (MethodReference method in type.Methods)
				Annotations.AddPreservedMethod (type, method.Resolve ());
		}
	}
}

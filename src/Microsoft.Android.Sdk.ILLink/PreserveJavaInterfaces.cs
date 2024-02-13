using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	class PreserveJavaInterfaces : BaseMarkHandler
	{
		public override void Initialize (LinkContext context, MarkContext markContext)
		{
			base.Initialize (context, markContext);
			markContext.RegisterMarkTypeAction (type => ProcessType (type));
		}

		bool IsActiveFor (AssemblyDefinition assembly)
		{
			return assembly.Name.Name == "Mono.Android" || assembly.MainModule.HasTypeReference ("Android.Runtime.IJavaObject");
		}

		void ProcessType (TypeDefinition type)
		{
			if (!IsActiveFor (type.Module.Assembly))
				return;

			// If we are preserving a Mono.Android interface,
			// preserve all members on the interface.
			if (!type.IsInterface)
				return;

			// Mono.Android interfaces will always inherit IJavaObject
			if (!type.ImplementsIJavaObject (cache))
				return;

			foreach (MethodReference method in type.Methods)
				Annotations.AddPreservedMethod (type, method.Resolve ());
		}
	}
}

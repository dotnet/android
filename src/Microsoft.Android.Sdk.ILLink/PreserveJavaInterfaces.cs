using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	class PreserveJavaInterfaces : IMarkHandler
	{
		LinkContext context;

		public void Initialize (LinkContext context, MarkContext markContext)
		{
			this.context = context;
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
			if (!type.ImplementsIJavaObject ())
				return;

			foreach (MethodReference method in type.Methods)
				context.Annotations.AddPreservedMethod (type, method.Resolve ());
		}
	}
}

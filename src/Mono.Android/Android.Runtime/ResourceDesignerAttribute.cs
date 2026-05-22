using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly)]
	public class ResourceDesignerAttribute : Attribute
	{
		const string UseResourceTypeConstructor = "Resource designer lookup by name requires unreferenced code. Use ResourceDesignerAttribute(Type) instead.";

		IResourceTypeProvider provider;

		public ResourceDesignerAttribute (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			Type resourceType)
		{
			provider = new TypeResourceTypeProvider (resourceType);
		}

		// Legacy bindings can pass namespace-qualified names such as "Xamarin.Kotlin.Resource",
		// so the string value cannot satisfy DynamicallyAccessedMembers unless it is assembly-qualified.
		[RequiresUnreferencedCode (UseResourceTypeConstructor)]
		public ResourceDesignerAttribute (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
				string fullName)
		{
			provider = new StringResourceTypeProvider (fullName);
		}

		public string FullName
		{
			get => provider.FullName;
			[RequiresUnreferencedCode (UseResourceTypeConstructor)]
			set => provider = new StringResourceTypeProvider (value);
		}

		[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		internal Type? GetResourceTypeFromAssembly (Assembly assembly) => provider.GetResourceTypeFromAssembly (assembly);

		public bool IsApplication { get; set; }

		private interface IResourceTypeProvider
		{
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			Type? GetResourceTypeFromAssembly (Assembly assembly);
			string FullName { get; }
		}

		private sealed class TypeResourceTypeProvider([DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)] Type resourceType) : IResourceTypeProvider
		{
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			public Type? GetResourceTypeFromAssembly (Assembly assembly) => assembly == resourceType.Assembly ? resourceType : null;
			public string FullName => resourceType.FullName ?? resourceType.Name;
		}

		[RequiresUnreferencedCode (UseResourceTypeConstructor)]
		private sealed class StringResourceTypeProvider(string fullName) : IResourceTypeProvider
		{
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			public Type? GetResourceTypeFromAssembly (Assembly assembly) => assembly.GetType (fullName);

			public string FullName => fullName;
		}
	}
}

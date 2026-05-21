using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly)]
	public class ResourceDesignerAttribute : Attribute
	{
		const string UseResourceTypeConstructor = "Resource designer lookup by name requires unreferenced code. Use ResourceDesignerAttribute(Type) instead.";

		readonly IResourceTypeProvider provider;

		public ResourceDesignerAttribute (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			Type resourceType)
		{
			provider = new TypeResourceTypeProvider (resourceType);
		}

		[RequiresUnreferencedCode (UseResourceTypeConstructor)]
		public ResourceDesignerAttribute (string fullName)
		{
			provider = new StringResourceTypeProvider (fullName);
		}

		public string FullName
		{
			get => provider.FullName;
			set => throw new NotSupportedException ("Resource designer lookup by name does not support setting the full name.");
		}

		[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		internal Type? GetResourceTypeFromAssembly (Assembly assembly) => provider.GetResourceTypeFromAssembly (assembly);

		public bool IsApplication { get; set; }

		private interface IResourceTypeProvider
		{
			Type? GetResourceTypeFromAssembly (Assembly assembly);
			string FullName { get; }
		}

		private sealed class TypeResourceTypeProvider([DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)] Type resourceType) : IResourceTypeProvider
		{
			public Type? GetResourceTypeFromAssembly (Assembly assembly) => assembly == resourceType.Assembly ? resourceType : null;
			public string FullName => resourceType.FullName ?? resourceType.Name;
		}

		[RequiresUnreferencedCode (UseResourceTypeConstructor)]
		private sealed class StringResourceTypeProvider(string fullName) : IResourceTypeProvider
		{
			public Type? GetResourceTypeFromAssembly (Assembly assembly) => assembly.GetType (fullName);
			public string FullName => fullName;
		}
	}
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly)]
	public class ResourceDesignerAttribute : Attribute
	{
		const string UseResourceTypeConstructor = "Resource designer lookup by name requires unreferenced code. Use ResourceDesignerAttribute(Type) instead.";

		readonly Type? resourceType;

		public ResourceDesignerAttribute (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
				Type resourceType)
		{
			if (resourceType == null)
				throw new ArgumentNullException (nameof (resourceType));

			this.resourceType = resourceType;
			FullName = resourceType.FullName ?? resourceType.Name;
		}

		[RequiresUnreferencedCode (UseResourceTypeConstructor)]
		public ResourceDesignerAttribute (string fullName)
		{
			FullName = fullName;
		}

		public string FullName { get; set; }

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		internal Type? GetResourceTypeFromAssembly (Assembly assembly)
		{
			if (resourceType != null) {
				return assembly == resourceType.Assembly ? resourceType : null;
			}

			const string legacyLookup = "The legacy string-based ResourceDesignerAttribute constructor requires unreferenced code.";

			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = legacyLookup)]
			[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = legacyLookup)]
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			static Type? AssemblyGetType (Assembly assembly, string name) => assembly.GetType (name);

			return AssemblyGetType (assembly, FullName);
		}

		public bool IsApplication { get; set; }
	}
}

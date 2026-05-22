using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly)]
	public class ResourceDesignerAttribute : Attribute
	{
		public ResourceDesignerAttribute (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
				string fullName)
		{
			FullName = fullName;
		}

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		public string FullName
		{
			get;
			set
			{
				resourceType = null;
				field = value;
			}
		}

		public bool IsApplication { get; set; }

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		private Type? resourceType;

		[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		internal Type? GetResourceTypeFromAssembly (Assembly assembly)
		{
			resourceType ??= Type.GetType (FullName, throwOnError: false);
			if (resourceType is not null)
			{
				if (resourceType.Assembly == assembly) {
					return resourceType;
				} else {
					return null; // no need to fallback to the assembly lookup if the type is found but in a different assembly
				}
			}

			// Fallback for when the type name is not an assembly-qualified name. If a non-AQN is passed to the constructor,
			// the trimmer will report the following warning:
			//
			//   warning IL2122: Type 'XYZ' is not assembly qualified. Type name strings used for dynamically accessing a type should be assembly qualified
			//
			// Since there is already a build warning, we can suppress the fallback warning.
			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Fallback for non-assembly-qualified type names. Warning is already emitted for non-AQN type names used in the constructor.")]
			[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = "Fallback for non-assembly-qualified type names. Warning is already emitted for non-AQN type names used in the constructor.")]
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			static Type? FallbackAssemblyGetType (Assembly a, string name) => a.GetType (name);

			return FallbackAssemblyGetType (assembly, FullName);
		}
	}
}

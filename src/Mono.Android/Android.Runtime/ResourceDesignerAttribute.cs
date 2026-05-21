using System;
using System.Diagnostics.CodeAnalysis;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly)]
	public class ResourceDesignerAttribute : Attribute
	{
		public ResourceDesignerAttribute (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
				Type resourceType)
		{
			if (resourceType == null)
				throw new ArgumentNullException (nameof (resourceType));

			ResourceType = resourceType;
			FullName = resourceType.FullName ?? resourceType.Name;
		}

		public ResourceDesignerAttribute (string fullName)
		{
			FullName = fullName;
		}

		public string FullName { get; set; }

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		public Type? ResourceType { get; set; }

		public bool IsApplication { get; set; }
	}
}

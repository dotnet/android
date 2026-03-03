using System;

namespace ApplicationUtility;

[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
class AspectExtractorAttribute : Attribute
{
	public Type ContainerAspectType { get; }
	public Type StoredAspectType { get; }

	public AspectExtractorAttribute (Type containerAspectType, Type storedAspectType)
	{
		ContainerAspectType = containerAspectType;
		StoredAspectType = storedAspectType;
	}
}

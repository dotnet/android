using System;

namespace ApplicationUtility;

/// <summary>
/// Marks a class as an extractor capable of extracting <see cref="StoredAspectType"/> from a <see cref="ContainerAspectType"/>.
/// Multiple instances may be applied to support extracting different aspect type combinations.
/// </summary>
[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
class AspectExtractorAttribute : Attribute
{
	public Type ContainerAspectType { get; }
	public Type StoredAspectType { get; }

	/// <summary>
	/// Initializes a new <see cref="AspectExtractorAttribute"/> with the given container and stored aspect types.
	/// </summary>
	/// <param name="containerAspectType">The type of the container aspect (e.g. a package).</param>
	/// <param name="storedAspectType">The type of the sub-aspect to be extracted.</param>
	public AspectExtractorAttribute (Type containerAspectType, Type storedAspectType)
	{
		ContainerAspectType = containerAspectType;
		StoredAspectType = storedAspectType;
	}
}

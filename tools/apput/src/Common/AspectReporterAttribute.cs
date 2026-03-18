using System;

namespace ApplicationUtility;

/// <summary>
/// Marks a class as a reporter for the specified <see cref="AspectType"/>.
/// Used by <see cref="Reporter"/> to discover the correct reporter implementation at runtime.
/// </summary>
[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
class AspectReporterAttribute : Attribute
{
	public Type AspectType { get; }

	/// <summary>
	/// Initializes a new <see cref="AspectReporterAttribute"/> for the given aspect type.
	/// </summary>
	/// <param name="aspectType">The type of aspect this reporter handles.</param>
	public AspectReporterAttribute (Type aspectType)
	{
		AspectType = aspectType;
	}
}

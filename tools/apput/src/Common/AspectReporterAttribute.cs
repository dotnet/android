using System;

namespace ApplicationUtility;

[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
class AspectReporterAttribute : Attribute
{
	public Type AspectType { get; }

	public AspectReporterAttribute (Type aspectType)
	{
		AspectType = aspectType;
	}
}

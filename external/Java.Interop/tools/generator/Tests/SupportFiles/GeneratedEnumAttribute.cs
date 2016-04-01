using System;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.ReturnValue | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
	public class GeneratedEnumAttribute : Attribute {}
}

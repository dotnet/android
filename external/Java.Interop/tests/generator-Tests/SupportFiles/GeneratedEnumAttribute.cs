#if !JAVA_INTEROP1

using System;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.ReturnValue | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
	public class GeneratedEnumAttribute : Attribute {}
}

#endif  // !JAVA_INTEROP1

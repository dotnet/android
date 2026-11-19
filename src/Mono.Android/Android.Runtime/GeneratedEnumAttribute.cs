using System;
using System.ComponentModel;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.ReturnValue | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
	[EditorBrowsable (EditorBrowsableState.Never)]
	public class GeneratedEnumAttribute : Attribute {}
}


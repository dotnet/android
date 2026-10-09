#nullable enable

using System;

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Method)]
	[Obsolete ("JniAddNativeMethodRegistrationAttribute is no longer supported and has no effect.")]
	public sealed class JniAddNativeMethodRegistrationAttribute : Attribute
	{
	}
}

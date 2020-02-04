#nullable enable

using System;

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Method)]
	public sealed class JniAddNativeMethodRegistrationAttribute : Attribute
	{
	}
}

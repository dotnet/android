using System;

namespace Android.Runtime
{
	// Field can be target too, but our toolchain doesn't generate any.
	[AttributeUsage (AttributeTargets.Parameter | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
	public class RequiresPermissionAttribute : Attribute
	{
		public RequiresPermissionAttribute (string value)
		{
			Value = value;
		}

		public string Value { get; set; }
	}
}


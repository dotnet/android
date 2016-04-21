using System;

namespace Android.Runtime
{
	// Field can be target too, but our toolchain doesn't generate any.
	[AttributeUsage (AttributeTargets.Parameter | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, AllowMultiple = true)]
	public class IntDefAttribute : Attribute
	{
		public bool Flag { get; set; }
		public string Type { get; set; }
		public string [] Fields { get; set; }
	}
}


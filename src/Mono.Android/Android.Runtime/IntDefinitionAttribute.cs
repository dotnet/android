using System;

namespace Android.Runtime
{
	[Obsolete ("IntDefinitionAttribute is no longer emitted by the bindings generator.")]
	[AttributeUsage (AttributeTargets.Field)]
	public class IntDefinitionAttribute : Attribute
	{
		public IntDefinitionAttribute (string? constantMember)
		{
			ConstantMember = constantMember;
		}

		public string? ConstantMember { get; set; }
		public string? JniField { get; set; }
	}
}

using System;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Field)]
	public class IntDefinitionAttribute : Attribute
	{
		public IntDefinitionAttribute (string constantMember)
		{
			ConstantMember = constantMember;
		}

		public string ConstantMember { get; set; }
		public string JniField { get; set; }
	}
}


using System;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
	public class NamespaceMappingAttribute : Attribute
	{
		public NamespaceMappingAttribute ()
		{
		}

		public string Java { get; set; }
		public string Managed { get; set; }
	}
}


using System;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly)]
	public class ResourceDesignerAttribute : Attribute
	{
		public ResourceDesignerAttribute (string fullName)
		{
			FullName = fullName;
		}

		public string FullName { get; set; }

		public bool IsApplication { get; set; }
	}
}

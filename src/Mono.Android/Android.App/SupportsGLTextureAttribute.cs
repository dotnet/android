using System;

namespace Android.App
{
	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class SupportsGLTextureAttribute : Attribute
	{
		public SupportsGLTextureAttribute (string name)
		{
			Name = name;
		}

		public string                 Name                    {get; private set;}
	}
}


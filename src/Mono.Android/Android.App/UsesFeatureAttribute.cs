using System;

namespace Android.App
{
	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class UsesFeatureAttribute : Attribute
	{
		public UsesFeatureAttribute ()
		{
		}

		public UsesFeatureAttribute (string name)
		{
			Name = name;
		}

		public string                 Name                    {get; private set;}
#if ANDROID_7
		public bool                   Required                {get; set;}
#endif
		public int                    GLESVersion             {get; set;}
	}
}


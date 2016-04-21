using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class UsesPermissionAttribute : Attribute {

		public UsesPermissionAttribute ()
		{
		}

		public UsesPermissionAttribute (string name)
		{
			Name = name;
		}

		public string                 Name                    {get; set;}
#if ANDROID_19
		public int                    MaxSdkVersion           {get; set;}
#endif
	}
}

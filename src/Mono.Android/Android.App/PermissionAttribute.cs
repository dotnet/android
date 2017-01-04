using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class PermissionAttribute : Attribute {

		public PermissionAttribute ()
		{
		}

		public string                 Description             {get; set;}
		public string                 Icon                    {get; set;}
		public string                 Label                   {get; set;}
		public string                 Name                    {get; set;}
		public string                 PermissionGroup         {get; set;}
		public Protection             ProtectionLevel         {get; set;}
#if ANDROID_25
		public string                 RoundIcon               {get; set;}
#endif
	}
}


using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly, 
			AllowMultiple=false, 
			Inherited=false)]
	public sealed partial class PermissionTreeAttribute : Attribute {

		public PermissionTreeAttribute ()
		{
		}

		public string                 Icon                    {get; set;}
		public string                 Label                   {get; set;}
		public string                 Name                    {get; set;}
#if ANDROID_25
		public string                 RoundIcon               {get; set;}
#endif
	}
}


using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
	public sealed partial class ServiceAttribute : Attribute {

		public ServiceAttribute ()
		{
		}

		public string                 Name                    {get; set;}

		public bool                   Enabled                 {get; set;}
		public bool                   Exported                {get; set;}
		public string                 Icon                    {get; set;}
#if ANDROID_16
		public bool                   IsolatedProcess         {get; set;}
#endif
		public string                 Label                   {get; set;}
		public string                 Permission              {get; set;}
		public string                 Process                 {get; set;}
	}
}

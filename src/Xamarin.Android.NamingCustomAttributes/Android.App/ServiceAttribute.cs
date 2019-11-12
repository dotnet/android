using System;
using System.ComponentModel;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class,
			AllowMultiple=false,
			Inherited=false)]
	public sealed partial class ServiceAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {

		public ServiceAttribute ()
		{
		}

		public string                 Name                    {get; set;}

#if ANDROID_24
		public bool                   DirectBootAware         {get; set;}
#endif
		public bool                   Enabled                 {get; set;}
		public bool                   Exported                {get; set;}
#if ANDROID_29
		public ForegroundService      ForegroundServiceType   {get; set;}
#endif
		[Category ("@drawable;@mipmap")]
		public string                 Icon                    {get; set;}
#if ANDROID_16
		public bool                   IsolatedProcess         {get; set;}
#endif
		[Category ("@string")]
		public string                 Label                   {get; set;}
		public string                 Permission              {get; set;}
		public string                 Process                 {get; set;}
#if ANDROID_25
		[Category ("@drawable;@mipmap")]
		public string                 RoundIcon               {get; set;}
#endif
	}
}

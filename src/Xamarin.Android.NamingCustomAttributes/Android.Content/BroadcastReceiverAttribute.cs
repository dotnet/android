using System;
using System.ComponentModel;

namespace Android.Content {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
	public partial class BroadcastReceiverAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {

		public BroadcastReceiverAttribute ()
		{
		}

		public bool                   DirectBootAware         {get; set;}
		public bool                   Enabled                 {get; set;}
		public bool                   Exported                {get; set;}
		[Category ("@string")]
		public string                 Description             {get; set;}
		[Category ("@drawable;@mipmap")]
		public string                 Icon                    {get; set;}
		[Category ("@string")]
		public string                 Label                   {get; set;}
		public string                 Name                    {get; set;}
		public string                 Permission              {get; set;}
		public string                 Process                 {get; set;}
#if ANDROID_25
		[Category ("@drawable;@mipmap")]
		public string                 RoundIcon               {get; set;}
#endif
	}
}

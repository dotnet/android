using System;
using System.ComponentModel;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class InstrumentationAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {

		public InstrumentationAttribute ()
		{
		}

		public bool                   FunctionalTest  {get; set;}
		public bool                   HandleProfiling {get; set;}
		[Category ("@drawable;@mipmap")]
		public string                 Icon            {get; set;}
		[Category ("@string")]
		public string                 Label           {get; set;}
		public string                 Name            {get; set;}
#if ANDROID_25
		[Category ("@drawable;@mipmap")]
		public string                 RoundIcon               {get; set;}
#endif
		public string                 TargetPackage   {get; set;}
#if ANDROID_26
		public string                 TargetProcesses {get; set;}
#endif
	}
}


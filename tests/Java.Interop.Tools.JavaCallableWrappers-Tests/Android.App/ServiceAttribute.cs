using System;

namespace Android.App {
	sealed partial class ServiceAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {
		public string                 Name                    {get; set;}
	}
}

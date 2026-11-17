using System;

namespace Android.App {
	sealed partial class ApplicationAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {
		public string                 Name                    {get; set;}
	}
}

using System;

namespace Android.App {
	sealed partial class InstrumentationAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {
		public string Name { get; set; }
	}
}


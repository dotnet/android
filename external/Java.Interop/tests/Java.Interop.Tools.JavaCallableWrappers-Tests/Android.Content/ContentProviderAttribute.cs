using System;

namespace Android.Content {

	partial class ContentProviderAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {
		public string Name { get; set; }
	}
}

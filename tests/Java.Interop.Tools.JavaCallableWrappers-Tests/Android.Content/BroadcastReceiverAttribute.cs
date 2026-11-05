using System;

namespace Android.Content {

	partial class BroadcastReceiverAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {
		public string Name { get; set; }
	}
}

using System;

namespace Android.Views
{
	partial class WindowManagerLayoutParams
	{
#if ANDROID_34
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0", "These flags are deprecated. Use WindowInsetsController instead.")]
		public SystemUiFlags SystemUiFlags {
#pragma warning disable CS0618 // Access the legacy property while exposing the correctly enumified replacement.
			get => (SystemUiFlags) SystemUiVisibility;
			set => SystemUiVisibility = (Android.Views.StatusBarVisibility) value;
#pragma warning restore CS0618
		}
#endif
	}
}

using System;

namespace Android.Views
{
	partial class WindowManagerLayoutParams
	{
#if ANDROID_34
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0", "These flags are deprecated. Use WindowInsetsController instead.")]
		public SystemUiFlags SystemUiFlags {
			get => (SystemUiFlags) SystemUiVisibility;
			set => SystemUiVisibility = (Android.Views.StatusBarVisibility) value;
		}
#endif
	}
}

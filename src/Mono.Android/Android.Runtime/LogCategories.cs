#if INSIDE_MONO_ANDROID_RUNTIME
using System;

namespace Android.Runtime
{
	// Keep in sync with the LogCategories enum in
	// monodroid/libmonodroid/logger.{c,h}
	[Flags]
	internal enum LogCategories {
		None      = 0,
		Default   = 1 << 0,
		Assembly  = 1 << 1,
		Debugger  = 1 << 2,
		GC        = 1 << 3,
		GlobalRef = 1 << 4,
		LocalRef  = 1 << 5,
		Timing    = 1 << 6,
		Bundle    = 1 << 7,
		Net       = 1 << 8,
		Netlink   = 1 << 9,
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME

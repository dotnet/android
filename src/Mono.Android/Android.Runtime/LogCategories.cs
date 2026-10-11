#if INSIDE_MONO_ANDROID_RUNTIME
using System;

namespace Android.Runtime
{
	// Keep in sync with the LogCategories enum in
	// external/Java.Interop/src/java-interop/java-interop-logger.h
	[Flags]
	internal enum LogCategories {
		None      = 0,
		Default   = 1 << 0,
		Assembly  = 1 << 1,
		// Bit 2 is reserved for the removed debugger logging category.
		GC        = 1 << 3,
		Timing    = 1 << 6,
		// Bit 7 is reserved for the removed bundle logging category.
		Net       = 1 << 8,
		// Bit 9 is reserved for the removed netlink logging category.
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME

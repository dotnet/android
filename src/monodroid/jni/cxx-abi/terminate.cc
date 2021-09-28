//
// Simple implementation of std::terminate() for Xamarin.Android
//
//  Does NOT support terminate handlers, since we don't use them.
//
#include <cstdlib>
#include <android/log.h>

namespace std {
	[[noreturn]] void
	terminate () noexcept
	{
		__android_log_write (ANDROID_LOG_FATAL, "monodroid", "std::terminate() called. Aborting.");
		abort ();
	}
}

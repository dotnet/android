#if !defined (__NATIVE_TRACING_HH)
#define __NATIVE_TRACING_HH

#include <string>
#include <jni.h>
#include <android/log.h>

#define UNW_LOCAL_ONLY
#include <libunwind.h>

// Public API must not expose any types that are part of libc++ - we don't know what version of the
// library (if any) is used by the application we're embedded in.
//
// For the same reason, we cannot return memory allocated with the `new` operator - the implementation
// used by the application's C++ code might be incompatible.  For this reason, any dynamically allocated
// memory we return to the caller is allocated with the libc's `malloc`
//
extern "C" {
	[[gnu::visibility("default")]]
	const char* xa_get_native_backtrace () noexcept;

	[[gnu::visibility("default")]]
	const char* xa_get_java_backtrace (JNIEnv *env) noexcept;

	[[gnu::visibility("default")]]
	const char* xa_get_interesting_signal_handlers () noexcept;
}
#endif // ndef __NATIVE_TRACING_HH

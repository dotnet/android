#ifndef __CPP_UTIL_HH
#define __CPP_UTIL_HH

#include <cstdarg>
#include <cstdlib>
#include <memory>

#if defined (ANDROID)
#include <android/log.h>
#else
#include <cstdio>
#endif

#include "cppcompat.hh"
#include "platform-compat.hh"

static inline void
do_abort_unless (bool condition, const char* fmt, ...)
{
	if (XA_LIKELY (condition)) {
		return;
	}

	va_list ap;

	va_start (ap, fmt);
#if defined (ANDROID)
	__android_log_vprint (ANDROID_LOG_FATAL, "monodroid", fmt, ap);
#else // def ANDROID
	vfprintf (stderr, fmt, ap);
	fprintf (stderr, "\n");
#endif // ndef ANDROID
	va_end (ap);

	std::abort ();
}

#define abort_unless(_condition_, _fmt_, ...) do_abort_unless (_condition_, "%s:%d (%s): " _fmt_, __FILE__, __LINE__, __FUNCTION__, ## __VA_ARGS__)
#define abort_if_invalid_pointer_argument(_ptr_) abort_unless ((_ptr_) != NULL, "Parameter '%s' must be a valid pointer", #_ptr_)
#define abort_if_negative_integer_argument(_arg_) abort_unless ((_arg_) > 0, "Parameter '%s' must be larger than 0", #_arg_)

namespace xamarin::android
{
	template <typename T>
	struct CDeleter final
	{
		void operator() (T* p)
		{
			std::free (p);
		}
	};

	template <typename T>
	using c_unique_ptr = std::unique_ptr<T, CDeleter<T>>;
}
#endif // !def __CPP_UTIL_HH

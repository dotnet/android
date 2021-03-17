#ifndef __JAVA_INTEROP_UTIL_H__
#define __JAVA_INTEROP_UTIL_H__

#include <cstdlib>

#ifdef _WINDOWS
/* Those two conversion functions are only properly implemented on Windows
 * because that's the only place where they should be useful.
 */
char* utf16_to_utf8 (const wchar_t *widestr);
wchar_t* utf8_to_utf16 (const char *mbstr);
#endif // def _WINDOWS

#include "java-interop-logger.h"

enum FatalExitCodes {
	FATAL_EXIT_CANNOT_FIND_MONO           =  1,
	FATAL_EXIT_ATTACH_JVM_FAILED          =  2,
	FATAL_EXIT_DEBUGGER_CONNECT           =  3,
	FATAL_EXIT_CANNOT_FIND_JNIENV         =  4,
	FATAL_EXIT_CANNOT_FIND_APK            = 10,
	FATAL_EXIT_TRIAL_EXPIRED              = 11,
	FATAL_EXIT_PTHREAD_FAILED             = 12,
	FATAL_EXIT_MISSING_ASSEMBLY           = 13,
	FATAL_EXIT_CANNOT_LOAD_BUNDLE         = 14,
	FATAL_EXIT_CANNOT_FIND_LIBMONOSGEN    = 15,
	FATAL_EXIT_NO_ASSEMBLIES              = 'A',
	FATAL_EXIT_MONO_MISSING_SYMBOLS       = 'B',
	FATAL_EXIT_FORK_FAILED                = 'F',
	FATAL_EXIT_MISSING_INIT               = 'I',
	FATAL_EXIT_MISSING_TIMEZONE_MEMBERS   = 'T',
	FATAL_EXIT_MISSING_ZIPALIGN           = 'Z',
	FATAL_EXIT_OUT_OF_MEMORY              = 'M',
	FATAL_EXIT_JVM_MISSING_SYMBOLS        = 'J',
};

static inline void*
_assert_valid_pointer (void *p, size_t size)
{
	if (!p) {
		if (size == 0) {
			/* In this case it's "ok" to return NULL, although a malloc
			 * implementation may choose to do something else
			 */
			return p;
		}

		log_fatal (LOG_DEFAULT, "Out of memory!");
		std::exit (FATAL_EXIT_OUT_OF_MEMORY);
	}

	return p;
}

static inline void*
xmalloc (size_t size)
{
	return _assert_valid_pointer (malloc (size), size);
}

static inline void*
xrealloc (void *ptr, size_t size)
{
	return _assert_valid_pointer (realloc (ptr, size), size);
}

static inline void*
xcalloc (size_t nmemb, size_t size)
{
	return _assert_valid_pointer (calloc (nmemb, size), nmemb * size);
}

#endif /* __JAVA_INTEROP_UTIL_H__ */

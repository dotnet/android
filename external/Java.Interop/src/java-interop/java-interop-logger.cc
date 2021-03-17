#include <stdio.h>
#include <stdarg.h>

#ifndef _MSC_VER
#include <strings.h>
#endif  // ndef _MSC_VER

#include "java-interop-logger.h"

#define LOG_VA_ARGS(_kind_,_category_,_format_)                                 \
	do {                                                                    \
		const char*     _kind     = (_kind_);                           \
		LogCategories   _cat      = (_category_);                       \
		va_list args;                                                   \
		va_start (args, _format_);                                      \
		log_vprint (_kind, CATEGORY_NAME (_cat), (_format_), args);  \
		va_end (args);                                                  \
	} while (0)

// Must match the same ordering as LogCategories
static const char* log_names[] = {
	"*none*",
	"javainterop",
	"javainterop-assembly",
	"javainterop-debug",
	"javainterop-gc",
	"javainterop-gref",
	"javainterop-lref",
	"javainterop-timing",
	"javainterop-bundle",
	"javainterop-network",
	"javainterop-netlink",
	"*error*",
};

#if defined(_MSC_VER)
#pragma intrinsic(_BitScanForward)
static inline unsigned long ffs (unsigned long value)
{
	unsigned long index;
	unsigned char isNonzero = _BitScanForward (&index, value);
	return isNonzero ? (index + 1) : 0;
}
#elif defined(__i386__) && defined(__GNUC__)
#define ffs(__value__) __builtin_ffs ((__value__))
#elif defined(__x86_64__) && defined(__GNUC__)
#define ffs(__value__) __builtin_ffsll ((__value__))
#endif

// ffs(value) returns index of lowest bit set in `value`
#define CATEGORY_NAME(value) (value == 0 ? log_names [0] : log_names [ffs (value)])

static void
log_vprint (const char* kind, const char* tag, const char* fmt, va_list ap)
{
  printf ("%s: [%s] ", kind, tag);
  vprintf (fmt, ap);
  putchar ('\n');
  fflush (stdout);
}

unsigned int log_categories = 0xFFFFFFFF;

void
log_error (LogCategories category, const char *format, ...)
{
	LOG_VA_ARGS ("error", category, format);
}

void
log_fatal (LogCategories category, const char *format, ...)
{
	LOG_VA_ARGS ("fatal error", category, format);
}

void
log_info_nocheck (LogCategories category, const char *format, ...)
{
	if ((log_categories & category) == 0)
		return;

	LOG_VA_ARGS ("info", category, format);
}

void
log_warn (LogCategories category, const char *format, ...)
{
	LOG_VA_ARGS ("warning", category, format);
}

void
log_debug_nocheck (LogCategories category, const char *format, ...)
{
	if ((log_categories & category) == 0)
		return;

	LOG_VA_ARGS ("debug", category, format);
}

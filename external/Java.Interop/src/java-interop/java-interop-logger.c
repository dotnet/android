#include <stdio.h>
#include <stdarg.h>

#include "java-interop-logger.h"

#define DO_LOG(_kind_,_category_,_format_,_args_)			\
	va_start ((_args_), (_format_));									                        \
	log_vprint (_kind_, CATEGORY_NAME((_category_)), (_format_), (_args_)); \
	va_end ((_args_));

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

#if defined(__i386__) && defined(__GNUC__)
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
	va_list args;

	DO_LOG ("error", category, format, args);
}

void
log_fatal (LogCategories category, const char *format, ...)
{
	va_list args;

	DO_LOG ("fatal error", category, format, args);
}

void
log_info_nocheck (LogCategories category, const char *format, ...)
{
	va_list args;

	if ((log_categories & category) == 0)
		return;

	DO_LOG ("info", category, format, args);
}

void
log_warn (LogCategories category, const char *format, ...)
{
	va_list args;

	DO_LOG ("warning", category, format, args);
}

void
log_debug_nocheck (LogCategories category, const char *format, ...)
{
	va_list args;

	if ((log_categories & category) == 0)
		return;

	DO_LOG ("debug", category, format, args);
}

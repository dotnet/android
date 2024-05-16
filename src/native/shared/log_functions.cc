#include <array>
#include <strings.h>

#include <android/log.h>

#include "java-interop-logger.h"
#include "log_types.hh"

// Must match the same ordering as LogCategories
static constexpr std::array<const char*, 12> log_names = {
	"*none*",
	"monodroid",
	"monodroid-assembly",
	"monodroid-debug",
	"monodroid-gc",
	"monodroid-gref",
	"monodroid-lref",
	"monodroid-timing",
	"monodroid-bundle",
	"monodroid-network",
	"monodroid-netlink",
	"*error*",
};

#if defined(__i386__) && defined(__GNUC__)
#define ffs(__value__) __builtin_ffs ((__value__))
#elif defined(__x86_64__) && defined(__GNUC__)
#define ffs(__value__) __builtin_ffsll ((__value__))
#endif

// ffs(value) returns index of lowest bit set in `value`
#define CATEGORY_NAME(value) (value == 0 ? log_names [0] : log_names [static_cast<size_t>(ffs (value))])

unsigned int log_categories = LOG_NONE;

#undef DO_LOG
#define DO_LOG(_level_,_category_,_format_,_args_)						                        \
	va_start ((_args_), (_format_));									                        \
	__android_log_vprint ((_level_), CATEGORY_NAME((_category_)), (_format_), (_args_)); \
	va_end ((_args_));

void
log_error (LogCategories category, const char *format, ...)
{
	va_list args;

	DO_LOG (ANDROID_LOG_ERROR, category, format, args);
}

void
log_fatal (LogCategories category, const char *format, ...)
{
	va_list args;

	DO_LOG (ANDROID_LOG_FATAL, category, format, args);
}

void
log_info_nocheck (LogCategories category, const char *format, ...)
{
	va_list args;

	if ((log_categories & category) == 0)
		return;

	DO_LOG (ANDROID_LOG_INFO, category, format, args);
}

void
log_warn (LogCategories category, const char *format, ...)
{
	va_list args;

	DO_LOG (ANDROID_LOG_WARN, category, format, args);
}

void
log_debug_nocheck (LogCategories category, const char *format, ...)
{
	va_list args;

	if ((log_categories & category) == 0)
		return;

	DO_LOG (ANDROID_LOG_DEBUG, category, format, args);
}

constexpr android_LogPriority DEFAULT_PRIORITY = ANDROID_LOG_INFO;

// relies on the fact that the LogLevel enum has sequential values
static constexpr android_LogPriority loglevel_map[] = {
	DEFAULT_PRIORITY, // Unknown
	DEFAULT_PRIORITY, // Default
	ANDROID_LOG_VERBOSE, // Verbose
	ANDROID_LOG_DEBUG, // Debug
	ANDROID_LOG_INFO, // Info
	ANDROID_LOG_WARN, // Warn
	ANDROID_LOG_ERROR, // Error
	ANDROID_LOG_FATAL, // Fatal
	ANDROID_LOG_SILENT, // Silent
};

static constexpr size_t loglevel_map_max_index = (sizeof(loglevel_map) / sizeof(android_LogPriority)) - 1;

namespace xamarin::android {
	void
	log_write (LogCategories category, LogLevel level, const char *message) noexcept
	{
		size_t map_index = static_cast<size_t>(level);
		android_LogPriority priority;

		if (map_index > loglevel_map_max_index) {
			priority = DEFAULT_PRIORITY;
		} else {
			priority = loglevel_map[map_index];
		}

		__android_log_write (priority, CATEGORY_NAME (category), message);
	}
}

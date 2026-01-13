#include <array>
#include <cstdarg>
#include <string_view>
#include <strings.h>

#include <android/log.h>

#include "java-interop-logger.h"
#include <shared/log_level.hh>
#include <constants.hh>

using namespace xamarin::android;

namespace {
	// Must match the same ordering as LogCategories
	constexpr std::array<std::string_view const, 12> log_names = {
		Constants::LOG_CATEGORY_NAME_NONE,
		Constants::LOG_CATEGORY_NAME_MONODROID,
		Constants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY,
		Constants::LOG_CATEGORY_NAME_MONODROID_DEBUG,
		Constants::LOG_CATEGORY_NAME_MONODROID_GC,
		Constants::LOG_CATEGORY_NAME_MONODROID_GREF,
		Constants::LOG_CATEGORY_NAME_MONODROID_LREF,
		Constants::LOG_CATEGORY_NAME_MONODROID_TIMING,
		Constants::LOG_CATEGORY_NAME_MONODROID_BUNDLE,
		Constants::LOG_CATEGORY_NAME_MONODROID_NETWORK,
		Constants::LOG_CATEGORY_NAME_MONODROID_NETLINK,
		Constants::LOG_CATEGORY_NAME_ERROR,
	};

	[[gnu::always_inline]]
	constexpr auto category_name (int value) noexcept -> const char*
	{
		if (value == 0) {
			return log_names[0].data ();
		}

		// ffs(value) returns index of lowest bit set in `value`
		return log_names [static_cast<size_t>(ffs (value))].data ();
	}

	constexpr android_LogPriority DEFAULT_PRIORITY = ANDROID_LOG_INFO;

	// relies on the fact that the LogLevel enum has sequential values
	constexpr android_LogPriority loglevel_map[] = {
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

	constexpr size_t loglevel_map_max_index = (sizeof(loglevel_map) / sizeof(android_LogPriority)) - 1;
}

unsigned int log_categories = LOG_NONE;

#undef DO_LOG
#define DO_LOG(_level_,_category_,_format_,_args_)						                        \
	va_start ((_args_), (_format_));									                        \
	__android_log_vprint ((_level_), category_name((_category_)), (_format_), (_args_)); \
	va_end ((_args_));

void
log_error (LogCategories category, const char *format, ...)
{
	va_list args;

	DO_LOG (ANDROID_LOG_ERROR, category, format, args);
}

void log_error_printf (LogCategories category, const char *format, ...) noexcept
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

void log_fatal_printf (LogCategories category, const char *format, ...) noexcept
{
	va_list args;

	DO_LOG (ANDROID_LOG_ERROR, category, format, args);
}

void
log_info_nocheck (LogCategories category, const char *format, ...)
{
	va_list args;

	if ((log_categories & category) != category) {
		return;
	}

	DO_LOG (ANDROID_LOG_INFO, category, format, args);
}

void
log_warn (LogCategories category, const char *format, ...)
{
	va_list args;

	DO_LOG (ANDROID_LOG_WARN, category, format, args);
}

// void log_warn_printf (LogCategories category, const char *format, ...) noexcept
// {
// 	va_list args;

// 	DO_LOG (ANDROID_LOG_ERROR, category, format, args);
// }

void
log_debug_nocheck (LogCategories category, const char *format, ...)
{
	va_list args;

	if ((log_categories & category) != category) {
		return;
	}

	DO_LOG (ANDROID_LOG_DEBUG, category, format, args);
}

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

		__android_log_write (priority, category_name (category), message);
	}
}

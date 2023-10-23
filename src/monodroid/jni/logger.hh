#ifndef __MONODROID_LOGGER_H__
#define __MONODROID_LOGGER_H__

#include <cstdlib>
#include <sys/system_properties.h>

#include "java-interop-logger.h"

#ifndef ANDROID
typedef enum android_LogPriority {
    ANDROID_LOG_UNKNOWN = 0,
    ANDROID_LOG_DEFAULT,    /* only for SetMinPriority() */
    ANDROID_LOG_VERBOSE,
    ANDROID_LOG_DEBUG,
    ANDROID_LOG_INFO,
    ANDROID_LOG_WARN,
    ANDROID_LOG_ERROR,
    ANDROID_LOG_FATAL,
    ANDROID_LOG_SILENT,     /* only for SetMinPriority(); must be last */
} android_LogPriority;
#endif

namespace xamarin::android::internal
{
	template<size_t MaxStackSize, typename TChar>
	class dynamic_local_string;
}

void init_logging_categories (xamarin::android::internal::dynamic_local_string<PROP_VALUE_MAX, char>& mono_log_mask, xamarin::android::internal::dynamic_local_string<PROP_VALUE_MAX, char>& mono_log_level) noexcept;

void init_reference_logging (const char *override_dir);

typedef enum _LogTimingCategories {
	LOG_TIMING_DEFAULT = 0,
	LOG_TIMING_BARE = 1 << 0,
	LOG_TIMING_FAST_BARE = 1 << 1,
} LogTimingCategories;

extern unsigned int log_timing_categories;

#if DEBUG
extern int gc_spew_enabled;
#endif

// Keep in sync with LogLevel defined in JNIEnv.cs
enum class LogLevel : unsigned int
{
	Unknown = 0x00,
	Default = 0x01,
	Verbose = 0x02,
	Debug   = 0x03,
	Info    = 0x04,
	Warn    = 0x05,
	Error   = 0x06,
	Fatal   = 0x07,
	Silent  = 0x08
};

// A slightly faster alternative to other log functions as it doesn't parse the message
// for format placeholders nor it uses variable arguments
void log_write (LogCategories category, LogLevel level, const char *message) noexcept;
#endif

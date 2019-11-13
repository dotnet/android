#ifndef __MONODROID_LOGGER_H__
#define __MONODROID_LOGGER_H__

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

void init_logging_categories ();

void init_reference_logging (const char *override_dir);

typedef enum _LogTimingCategories {
	LOG_TIMING_DEFAULT = 0,
	LOG_TIMING_BARE = 1 << 0,
} LogTimingCategories;

extern unsigned int log_timing_categories;

#if DEBUG
extern int gc_spew_enabled;
#endif

#endif

#pragma once

#include <stdarg.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum android_LogPriority {
	ANDROID_LOG_UNKNOWN = 0,
	ANDROID_LOG_DEFAULT,
	ANDROID_LOG_VERBOSE,
	ANDROID_LOG_DEBUG,
	ANDROID_LOG_INFO,
	ANDROID_LOG_WARN,
	ANDROID_LOG_ERROR,
	ANDROID_LOG_FATAL,
	ANDROID_LOG_SILENT,
} android_LogPriority;

int __android_log_write (int priority, const char *tag, const char *text);
int __android_log_vprint (int priority, const char *tag, const char *format, va_list args);

#ifdef __cplusplus
}
#endif

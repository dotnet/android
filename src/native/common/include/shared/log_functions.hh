#pragma once

#include <cstdarg>

#include "java-interop-logger.h"
#include <shared/log_level.hh>

namespace xamarin::android {
	// A slightly faster alternative to other log functions as it doesn't parse the message
	// for format placeholders nor it uses variable arguments
	void log_write (LogCategories category, LogLevel level, const char *message) noexcept;
	void log_writev (LogCategories category, LogLevel level, const char *format, va_list args) noexcept;
	void log_writef (LogCategories category, LogLevel level, const char *format, ...) noexcept __attribute__ ((format (printf, 3, 4)));
	void log_debugf (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
	void log_infof (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
	void log_warnf (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
	void log_errorf (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
}

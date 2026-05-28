#pragma once

#include <cstdarg>
#include <cstdint>
#include <string>
#include <string_view>

#include "java-interop-logger.h"
#include <shared/log_level.hh>

// We redeclare macros here
#if defined(log_debug)
#undef log_debug
#endif

#if defined(log_info)
#undef log_info
#endif

#define DO_LOG_FMT(_level, _category_, _fmt_, ...)                   \
	do {                                                            \
		if ((log_categories & ((_category_))) != 0) {               \
			::xamarin::android::log_ ## _level ## _fmt ((_category_), _fmt_ __VA_OPT__(,) __VA_ARGS__); \
		}                                                           \
	} while (0)

// NOTE: _fmt_ takes arguments in POSIX printf style.
#define log_debug(_category_, _fmt_, ...) DO_LOG_FMT (debug, (_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in POSIX printf style.
#define log_info(_category_, _fmt_, ...) DO_LOG_FMT (info, (_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in POSIX printf style.
#define log_warn(_category_, _fmt_, ...) ::xamarin::android::log_warn_fmt ((_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in POSIX printf style.
#define log_error(_category_, _fmt_, ...) ::xamarin::android::log_error_fmt ((_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in POSIX printf style.
#define log_fatal(_category_, _fmt_, ...) ::xamarin::android::log_fatal_fmt ((_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

namespace xamarin::android {
	// A slightly faster alternative to other log functions as it doesn't parse the message
	// for format placeholders nor it uses variable arguments
	void log_write (LogCategories category, LogLevel level, const char *message) noexcept;
	void log_writev (LogCategories category, LogLevel level, const char *format, va_list args) noexcept;
	void log_write_fmt (LogCategories category, LogLevel level, const char *format, ...) noexcept __attribute__ ((format (printf, 3, 4)));
	void log_debug_fmt (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
	void log_info_fmt (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
	void log_warn_fmt (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
	void log_error_fmt (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
	void log_fatal_fmt (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));

	[[gnu::always_inline]]
	static inline void log_write (LogCategories category, LogLevel level, std::string_view const& message) noexcept
	{
		log_write (category, level, message.data ());
	}

	[[gnu::always_inline]]
	static inline void log_debug_fmt (LogCategories category, std::string_view const& message) noexcept
	{
		log_write (category, LogLevel::Debug, message);
	}

	[[gnu::always_inline]]
	static inline void log_info_fmt (LogCategories category, std::string_view const& message) noexcept
	{
		log_write (category, LogLevel::Info, message);
	}

	[[gnu::always_inline]]
	static inline void log_warn_fmt (LogCategories category, std::string_view const& message) noexcept
	{
		log_write (category, LogLevel::Warn, message);
	}

	[[gnu::always_inline]]
	static inline void log_error_fmt (LogCategories category, std::string_view const& message) noexcept
	{
		log_write (category, LogLevel::Error, message);
	}

	[[gnu::always_inline]]
	static inline void log_fatal_fmt (LogCategories category, std::string_view const& message) noexcept
	{
		log_write (category, LogLevel::Fatal, message);
	}

	[[gnu::always_inline]]
	static inline void log_debug_fmt (LogCategories category, std::string const& message) noexcept
	{
		log_debug_fmt (category, std::string_view { message });
	}

	[[gnu::always_inline]]
	static inline void log_info_fmt (LogCategories category, std::string const& message) noexcept
	{
		log_info_fmt (category, std::string_view { message });
	}

	[[gnu::always_inline]]
	static inline void log_warn_fmt (LogCategories category, std::string const& message) noexcept
	{
		log_warn_fmt (category, std::string_view { message });
	}

	[[gnu::always_inline]]
	static inline void log_error_fmt (LogCategories category, std::string const& message) noexcept
	{
		log_error_fmt (category, std::string_view { message });
	}

	[[gnu::always_inline]]
	static inline void log_fatal_fmt (LogCategories category, std::string const& message) noexcept
	{
		log_fatal_fmt (category, std::string_view { message });
	}
}

[[gnu::always_inline]]
static inline constexpr void log_debug_nocheck (LogCategories category, std::string_view const& message) noexcept
{
	xamarin::android::log_write (category, xamarin::android::LogLevel::Debug, message.data ());
}

[[gnu::always_inline]]
static inline constexpr void log_info_nocheck (LogCategories category, std::string_view const& message) noexcept
{
	xamarin::android::log_write (category, xamarin::android::LogLevel::Info, message.data ());
}

static inline void log_debug_nocheck_fmt (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
static inline void log_debug_nocheck_fmt (LogCategories category, const char *format, ...) noexcept
{
	va_list args;
	va_start (args, format);
	xamarin::android::log_writev (category, xamarin::android::LogLevel::Debug, format, args);
	va_end (args);
}

static inline void log_info_nocheck_fmt (LogCategories category, const char *format, ...) noexcept __attribute__ ((format (printf, 2, 3)));
static inline void log_info_nocheck_fmt (LogCategories category, const char *format, ...) noexcept
{
	va_list args;
	va_start (args, format);
	xamarin::android::log_writev (category, xamarin::android::LogLevel::Info, format, args);
	va_end (args);
}

extern unsigned int log_categories;

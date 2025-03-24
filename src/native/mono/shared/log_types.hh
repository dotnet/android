#pragma once

#include <cstdint>
#include <format>
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
			::log_ ## _level ## _nocheck_fmt ((_category_), _fmt_ __VA_OPT__(,) __VA_ARGS__); \
		}                                                           \
	} while (0)

//
// For std::format spec, see https://en.cppreference.com/w/cpp/utility/format/spec
//

// NOTE: _fmt_ takes arguments in the std::format style not the POSIX printf style
#define log_debug(_category_, _fmt_, ...) DO_LOG_FMT (debug, (_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in the std::format style not the POSIX printf style
#define log_info(_category_, _fmt_, ...) DO_LOG_FMT (info, (_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in the std::format style not the POSIX printf style
#define log_warn(_category_, _fmt_, ...) log_warn_fmt ((_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in the std::format style not the POSIX printf style
#define log_error(_category_, _fmt_, ...) log_error_fmt ((_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

// NOTE: _fmt_ takes arguments in the std::format style not the POSIX printf style
#define log_fatal(_category_, _fmt_, ...) log_fatal_fmt ((_category_), (_fmt_) __VA_OPT__(,) __VA_ARGS__)

namespace xamarin::android {
	// A slightly faster alternative to other log functions as it doesn't parse the message
	// for format placeholders nor it uses variable arguments
	void log_write (LogCategories category, LogLevel level, const char *message) noexcept;
}

template<typename ...Args> [[gnu::always_inline]]
static inline constexpr void log_debug_nocheck_fmt (LogCategories category, std::format_string<Args...> fmt, Args&& ...args)
{
	log_write (category, xamarin::android::LogLevel::Debug, std::format (fmt, std::forward<Args>(args)...).c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_debug_nocheck (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Debug, message.data ());
}

template<typename ...Args> [[gnu::always_inline]]
static inline constexpr void log_info_nocheck_fmt (LogCategories category, std::format_string<Args...> fmt, Args&& ...args)
{
	log_write (category, xamarin::android::LogLevel::Info, std::format (fmt, std::forward<Args>(args)...).c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_info_nocheck (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Info, message.data ());
}

template<typename ...Args> [[gnu::always_inline]]
static inline constexpr void log_warn_fmt (LogCategories category, std::format_string<Args...> fmt, Args&& ...args) noexcept
{
	log_write (category, xamarin::android::LogLevel::Warn, std::format (fmt, std::forward<Args>(args)...).c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_warn_fmt (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Warn, message.data ());
}

template<typename ...Args> [[gnu::always_inline]]
static inline constexpr void log_error_fmt (LogCategories category, std::format_string<Args...> fmt, Args&& ...args) noexcept
{
	log_write (category, xamarin::android::LogLevel::Error, std::format (fmt, std::forward<Args>(args)...).c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_error_fmt (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Error, message.data ());
}

template<typename ...Args> [[gnu::always_inline]]
static inline constexpr void log_fatal_fmt (LogCategories category, std::format_string<Args...> fmt, Args&& ...args) noexcept
{
	log_write (category, xamarin::android::LogLevel::Fatal, std::format (fmt, std::forward<Args>(args)...).c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_fatal_fmt (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Fatal, message.data ());
}

extern unsigned int log_categories;

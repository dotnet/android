#pragma once

#include <cstdint>
#include <format>
#include <string>
#include <string_view>

#include "java-interop-logger.h"

// We redeclare macros as real functions here
#if defined(DO_LOG)
#undef DO_LOG
#endif

#if defined(log_debug)
#undef log_debug
#endif

#if defined(log_info)
#undef log_info
#endif

namespace xamarin::android {
	namespace detail {
		[[gnu::always_inline]]
		static inline bool _category_is_enabled (LogCategories category) noexcept
		{
			return (log_categories & category) == category;
		}
	}

	enum class LogTimingCategories : uint32_t
	{
		Default  = 0,
		Bare     = 1 << 0,
		FastBare = 1 << 1,
	};

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

	template<typename ...Args> [[gnu::always_inline]]
	static inline constexpr void log_debug (LogCategories category, const char *format, Args&& ...args) noexcept
	{
		if (detail::_category_is_enabled (category)) {
			::log_debug_nocheck (category, format, std::forward<Args>(args)...);
		}
	}

	template<typename ...Args> [[gnu::always_inline]]
	static inline constexpr void log_debug (LogCategories category, std::string_view const& format, Args&& ...args) noexcept
	{
		if (detail::_category_is_enabled (category)) {
			::log_debug_nocheck (category, format.data (), std::forward<Args>(args)...);
		}
	}

	static inline constexpr void log_debug (LogCategories category, std::string const& message) noexcept
	{
		if (detail::_category_is_enabled (category)) {
			::log_debug_nocheck (category, message.c_str ());
		}
	}

	//
	// This will be enabled once all log_* calls are converted to std::format format
	//
	// template<typename ...Args> [[gnu::always_inline]]
	// static inline constexpr void log_debug (LogCategories category, std::format_string<Args...> fmt, Args&& ...args)
	// {
	// 	if (detail::_category_is_enabled (category)) {

	// 		log_debug_nocheck (category, std::format (fmt, std::forward<Args>(args)...).c_str ());
	// 	}
	// }

	template<typename ...Args> [[gnu::always_inline]]
	static inline constexpr void log_info (LogCategories category, const char *format, Args&& ...args) noexcept
	{
		if (detail::_category_is_enabled (category)) {
			::log_info_nocheck (category, format, std::forward<Args>(args)...);
		}
	}

	template<typename ...Args> [[gnu::always_inline]]
	static inline constexpr void log_info (LogCategories category, std::string_view const& format, Args&& ...args) noexcept
	{
		if (detail::_category_is_enabled (category)) {
			::log_info_nocheck (category, format.data (), std::forward<Args>(args)...);
		}
	}

	[[gnu::always_inline]]
	static inline constexpr void log_info (LogCategories category, std::string const& message) noexcept
	{
		if (detail::_category_is_enabled (category)) {
			::log_info_nocheck (category, message.c_str ());
		}
	}

	[[gnu::always_inline]]
	static inline constexpr void log_info_nocheck (LogCategories category, std::string const& message) noexcept
	{
		if (detail::_category_is_enabled (category)) {
			::log_info_nocheck (category, message.c_str ());
		}
	}

	[[gnu::always_inline]]
	static inline constexpr void log_warn (LogCategories category, std::string const& message) noexcept
	{
		::log_warn (category, message.c_str ());
	}

	[[gnu::always_inline]]
	static inline constexpr void log_warn (LogCategories category, std::string_view const& message) noexcept
	{
		::log_warn (category, message.data ());
	}

	[[gnu::always_inline]]
	static inline constexpr void log_error (LogCategories category, std::string const& message) noexcept
	{
		::log_error (category, message.c_str ());
	}

	[[gnu::always_inline]]
	static inline constexpr void log_error (LogCategories category, std::string_view const& message) noexcept
	{
		::log_error (category, message.data ());
	}

	[[gnu::always_inline]]
	static inline constexpr void log_fatal (LogCategories category, std::string const& message) noexcept
	{
		::log_fatal (category, message.c_str ());
	}

	[[gnu::always_inline]]
	static inline constexpr void log_fatal (LogCategories category, std::string_view const& message) noexcept
	{
		::log_fatal (category, message.data ());
	}
}

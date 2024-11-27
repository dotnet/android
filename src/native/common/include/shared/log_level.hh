#pragma once

#include <cstdint>
#include <format>
#include <string>
#include <string_view>

#include "java-interop-logger.h"

// We redeclare macros here
#if defined(log_debug)
#undef log_debug
#endif

#if defined(log_info)
#undef log_info
#endif

#define DO_LOG_FMT(_level, _category_, _message_)                   \
	do {                                                            \
		if ((log_categories & ((_category_))) != 0) {               \
			::log_ ## _level ## _nocheck ((_category_), _message_); \
		}                                                           \
	} while (0)

#define log_debug(_category_, _message_) DO_LOG_FMT (debug, (_category_), (_message_))
#define log_info(_category_, _message_) DO_LOG_FMT (info, (_category_), (_message_))

namespace xamarin::android {
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

[[gnu::always_inline]]
static inline constexpr void log_debug_nocheck (LogCategories category, std::string const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Debug, message.c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_debug_nocheck (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Debug, message.data ());
}

[[gnu::always_inline]]
static inline constexpr void log_info_nocheck (LogCategories category, std::string const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Info, message.c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_info_nocheck (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Info, message.data ());
}

[[gnu::always_inline]]
static inline constexpr void log_warn (LogCategories category, std::string const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Warn, message.c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_warn (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Warn, message.data ());
}

[[gnu::always_inline]]
static inline constexpr void log_error (LogCategories category, std::string const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Error, message.c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_error (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Error, message.data ());
}

[[gnu::always_inline]]
static inline constexpr void log_fatal (LogCategories category, std::string const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Fatal, message.c_str ());
}

[[gnu::always_inline]]
static inline constexpr void log_fatal (LogCategories category, std::string_view const& message) noexcept
{
	log_write (category, xamarin::android::LogLevel::Fatal, message.data ());
}

extern unsigned int log_categories;
#endif // ndef LOG_LEVEL_HH

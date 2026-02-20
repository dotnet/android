#pragma once

#include <string>

#if defined(XA_HOST_NATIVEAOT)
#include <cerrno>
#include <cstdio>
#include <cstdarg>
#else
#include <format>
#endif

#include <runtime-base/strings.hh>

namespace xamarin::android {
#if defined(XA_HOST_NATIVEAOT)
	[[gnu::always_inline]]
	static inline constexpr std::string format_string (const char *format, ...) noexcept
	{
		constexpr size_t BufferSize = 256; // Hopefully enough for most uses, to prevent dynamic allocation
		dynamic_local_string<BufferSize> dest;

		// Duplicates some code from format_printf in strings.hh, but it's not something we can avoid without
		// obscuring the code, due to pecularities of how stdargs work (va_list is in undefined state after
		// returning from the v* functions taking it as a parameter instead of ...)
		va_list args;
		va_start (args, format);
		int n = vsnprintf (dest.get (), dest.size (), format, args);
		va_end (args);

		auto log_with_errno = [](const char *msg) {
			// Logging with separate calls to avoid memory allocation, we might be OOM-ing
			log_write (LOG_DEFAULT, LogLevel::Warn, msg);
			log_write (LOG_DEFAULT, LogLevel::Warn, strerror (errno));
		};

		if (n < 0) [[unlikely]] {
			log_with_errno ("Failed to format string");
			return "<error:1>";
		}

		auto res = static_cast<size_t>(n);
		if (res < dest.size ()) [[likely]] {
			return std::string (dest.get (), dest.length ());
		}

		// resize_for_extra adds one more byte for the NUL character
		dest.resize_for_extra (res - dest.size ());
		if (dest.size () <= res) {
			log_write (LOG_DEFAULT, LogLevel::Warn, "Failed to format string, buffer resize failed.");
			return "<error:2>";
		}

		va_start (args, format);
		n = vsnprintf (dest.get (), dest.size (), format, args);
		va_end (args);

		if (n < 0) [[unlikely]] {
			log_with_errno ("Failed to format string after resize");
			return "<error:3>";
		}

		return std::string (dest.get (), dest.length ());
	}
#else
	template<typename ...Args> [[gnu::always_inline]]
	static inline constexpr std::string format_string (std::format_string<Args...> fmt, Args&& ...args) noexcept
	{
		return std::format (fmt, std::forward<Args>(args)...).c_str ();
	}
#endif
}

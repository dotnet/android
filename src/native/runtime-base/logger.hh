#ifndef __MONODROID_LOGGER_H__
#define __MONODROID_LOGGER_H__

#include <string_view>

#include "java-interop-logger.h"
#include "strings.hh"

namespace xamarin::android {
	enum class LogTimingCategories : uint32_t
	{
		Default  = 0,
		Bare     = 1 << 0,
		FastBare = 1 << 1,
	};

	class Logger
	{
	public:
		static void init_logging_categories (char*& mono_log_mask, char*& mono_log_level) noexcept;
		static void init_reference_logging (const char *override_dir) noexcept;

#if defined(DEBUG)
		static void set_debugger_log_level (const char *level) noexcept;

		static bool have_debugger_log_level () noexcept
		{
			return _got_debugger_log_level;
		}

		static int get_debugger_log_level () noexcept
		{
			return _debugger_log_level;
		}
#endif // def DEBUG

	private:
		static bool set_category (std::string_view const& name, internal::string_segment& arg, unsigned int entry, bool arg_starts_with_name = false) noexcept;

	private:
		static inline LogTimingCategories _log_timing_categories;
#if defined(DEBUG)
		static inline bool _got_debugger_log_level = false;
		static inline int _debugger_log_level = 0;
		static inline int _gc_spew_enabled;
#endif // def DEBUG
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
#endif

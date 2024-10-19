#ifndef __MONODROID_LOGGER_H__
#define __MONODROID_LOGGER_H__

#include <string_view>

#include "log_types.hh"
#include "strings.hh"

namespace xamarin::android {
	class Logger
	{
	public:
		static void init_logging_categories (char*& mono_log_mask, char*& mono_log_level) noexcept;
		static void init_reference_logging (const char *override_dir) noexcept;

		static LogTimingCategories log_timing_categories () noexcept
		{
			return _log_timing_categories;
		}

		static bool native_tracing_enabled () noexcept
		{
			return _native_tracing_enabled;
		}

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

		static void set_gc_spew_enabled (int yesno) noexcept
		{
			_gc_spew_enabled = yesno;
		}

		static int gc_spew_enabled () noexcept
		{
			return _gc_spew_enabled;
		}
#endif // def DEBUG

	private:
		static bool set_category (std::string_view const& name, internal::string_segment& arg, unsigned int entry, bool arg_starts_with_name = false) noexcept;

	private:
		static inline LogTimingCategories _log_timing_categories = LogTimingCategories::Default;
		static inline bool _native_tracing_enabled = false;
#if defined(DEBUG)
		static inline bool _got_debugger_log_level = false;
		static inline int _debugger_log_level = 0;
		static inline int _gc_spew_enabled = 0;
#endif // def DEBUG
	};
}
#endif

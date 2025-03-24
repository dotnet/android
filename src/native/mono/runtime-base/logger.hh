#ifndef __MONODROID_LOGGER_H__
#define __MONODROID_LOGGER_H__

#include <string_view>

#include "log_types.hh"
#include <runtime-base/strings.hh>

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

		static void set_gc_spew_enabled (bool yesno) noexcept
		{
			_gc_spew_enabled = yesno;
		}

		static bool gc_spew_enabled () noexcept
		{
			return _gc_spew_enabled;
		}

	private:
		static bool set_category (std::string_view const& name, string_segment& arg, unsigned int entry, bool arg_starts_with_name = false) noexcept;

	private:
		static inline LogTimingCategories _log_timing_categories;
#if defined(DEBUG)
		static inline bool _got_debugger_log_level = false;
		static inline int _debugger_log_level = 0;
#endif // def DEBUG
		static inline bool _gc_spew_enabled = false;
	};
}
#endif

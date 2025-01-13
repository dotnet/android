#pragma once

#include <cstdio>

#include <string_view>

#include <shared/log_types.hh>
#include "strings.hh"

namespace xamarin::android {
	class Logger
	{
	public:
		static void init_logging_categories () noexcept;
		static void init_reference_logging (std::string_view const& override_dir) noexcept;

		static auto log_timing_categories () noexcept -> LogTimingCategories
		{
			return _log_timing_categories;
		}

		static void set_gc_spew_enabled (bool yesno) noexcept
		{
			_gc_spew_enabled = yesno;
		}

		static auto gc_spew_enabled () noexcept -> bool
		{
			return _gc_spew_enabled;
		}

		static auto gref_log () -> FILE*
		{
			return _gref_log;
		}

		static auto lref_log () -> FILE*
		{
			return _lref_log;
		}

		static auto gref_to_logcat () -> bool
		{
			return _gref_to_logcat;
		}

		static auto lref_to_logcat () -> bool
		{
			return _lref_to_logcat;
		}

	private:
		static bool set_category (std::string_view const& name, string_segment& arg, unsigned int entry, bool arg_starts_with_name = false) noexcept;

	private:
		static inline LogTimingCategories _log_timing_categories;
		static inline bool  _gc_spew_enabled = false;
		static inline FILE *_gref_log = nullptr;
		static inline FILE *_lref_log = nullptr;
		static inline bool  _gref_to_logcat = false;
		static inline bool  _lref_to_logcat = false;
	};
}

#include <array>
#include <cstdarg>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <strings.h>

#include <android/log.h>
#include <mono/utils/mono-publib.h>

#include "android-system.hh"
#include <shared/cpp-util.hh>
#include <shared/log_level.hh>
#include "logger.hh"
#include "shared-constants.hh"
#include "util.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

namespace {
	const char *gref_file = nullptr;
	const char *lref_file = nullptr;
	const char *reference_log_dir = nullptr;
	bool light_gref  = false;
	bool light_lref  = false;
	bool gref_to_logcat = false;
	bool lref_to_logcat = false;
}

#if defined(DEBUG)
void
Logger::set_debugger_log_level (const char *level) noexcept
{
	if (level == nullptr || *level == '\0') {
		_got_debugger_log_level = false;
		return;
	}

	unsigned long v = strtoul (level, nullptr, 0);
	if (v == std::numeric_limits<unsigned long>::max () && errno == ERANGE) {
		log_error (LOG_DEFAULT, "Invalid debugger log level value '{}', expecting a positive integer or zero", level);
		return;
	}

	if (v > std::numeric_limits<int>::max ()) {
		log_warn (LOG_DEFAULT, "Debugger log level value is higher than the maximum of {}, resetting to the maximum value.", std::numeric_limits<int>::max ());
		v = std::numeric_limits<int>::max ();
	}

	_got_debugger_log_level = true;
	_debugger_log_level = static_cast<int>(v);
}
#endif // def DEBUG

void
Logger::init_reference_logging (const char *override_dir) noexcept
{
	reference_log_dir = override_dir;
}

const char*
Logger::gref_log_path () noexcept
{
	return gref_file;
}

const char*
Logger::lref_log_path () noexcept
{
	return lref_file;
}

const char*
Logger::reference_log_directory () noexcept
{
	return reference_log_dir;
}

bool
Logger::light_gref_enabled () noexcept
{
	return light_gref;
}

bool
Logger::light_lref_enabled () noexcept
{
	return light_lref;
}

bool
Logger::gref_to_logcat_enabled () noexcept
{
	return gref_to_logcat;
}

bool
Logger::lref_to_logcat_enabled () noexcept
{
	return lref_to_logcat;
}

bool
Logger::gref_enabled () noexcept
{
	return (log_categories & LOG_GREF) != 0;
}

[[gnu::always_inline]] bool
Logger::set_category (std::string_view const& name, string_segment& arg, unsigned int entry, bool arg_starts_with_name) noexcept
{
	if ((log_categories & entry) == entry) {
		return false;
	}

	if (arg_starts_with_name ? arg.starts_with (name) : arg.equal (name)) {
		log_categories |= entry;
		return true;
	}

	return false;
}

void
Logger::init_logging_categories (char*& mono_log_mask, char*& mono_log_level) noexcept
{
	mono_log_mask = nullptr;
	mono_log_level = nullptr;
	_log_timing_categories = LogTimingCategories::Default;

	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> value;
	if (AndroidSystem::monodroid_get_system_property (SharedConstants::DEBUG_MONO_LOG_PROPERTY, value) == 0)
		return;

	string_segment param;
	while (value.next_token (',', param)) {
		constexpr std::string_view CAT_ALL { "all" };

		if (param.equal (CAT_ALL)) {
			log_categories = 0xFFFFFFFF;
			break;
		}

		if (set_category ("assembly", param, LOG_ASSEMBLY)) {
			continue;
		}

		if (set_category ("default",  param, LOG_DEFAULT)) {
			continue;
		}

		if (set_category ("debugger", param, LOG_DEBUGGER))  {
			continue;
		}

		if (set_category ("gc",       param, LOG_GC)) {
			continue;
		}

		if (set_category ("gref",     param, LOG_GREF)) {
			continue;
		}

		if (set_category ("lref",     param, LOG_LREF)) {
			continue;
		}

		if (set_category ("timing",   param, LOG_TIMING)) {
			continue;
		}

		if (set_category ("network",  param, LOG_NET)) {
			continue;
		}

		if (set_category ("netlink",  param, LOG_NETLINK)) {
			continue;
		}

		constexpr std::string_view CAT_GREF_EQUALS { "gref=" };
		if (set_category (CAT_GREF_EQUALS, param, LOG_GREF, true /* arg_starts_with_name */)) {
			gref_file = Util::strdup_new (param, CAT_GREF_EQUALS.length ());
			continue;
		}

		if (set_category ("gref-", param, LOG_GREF)) {
			light_gref = true;
			continue;
		}

		if (set_category ("gref+", param, LOG_GREF)) {
			gref_to_logcat = true;
			continue;
		}

		constexpr std::string_view CAT_LREF_EQUALS { "lref=" };
		if (set_category (CAT_LREF_EQUALS, param, LOG_LREF, true /* arg_starts_with_name */)) {
			lref_file = Util::strdup_new (param, CAT_LREF_EQUALS.length ());
			continue;
		}

		if (set_category ("lref-", param, LOG_LREF)) {
			light_lref = true;
			continue;
		}

		if (set_category ("lref+", param, LOG_LREF)) {
			lref_to_logcat = true;
			continue;
		}

		if (param.starts_with ("timing=fast-bare")) {
			log_categories |= LOG_TIMING;
			_log_timing_categories |= LogTimingCategories::FastBare;
			continue;
		}

		if (param.starts_with ("timing=bare")) {
			log_categories |= LOG_TIMING;
			_log_timing_categories |= LogTimingCategories::Bare;
			continue;
		}

		constexpr std::string_view MONO_LOG_MASK_ARG { "mono_log_mask=" };
		if (param.starts_with (MONO_LOG_MASK_ARG)) {
			mono_log_mask = Util::strdup_new (param, MONO_LOG_MASK_ARG.length ());
			continue;
		}

		constexpr std::string_view MONO_LOG_LEVEL_ARG { "mono_log_level=" };
		if (param.starts_with (MONO_LOG_LEVEL_ARG)) {
			mono_log_level = Util::strdup_new (param, MONO_LOG_LEVEL_ARG.length ());
			continue;
		}

#if defined (DEBUG)
		constexpr std::string_view DEBUGGER_LOG_LEVEL { "debugger-log-level=" };
		if (param.starts_with (DEBUGGER_LOG_LEVEL)) {
			dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> level;
			level.assign (param.start () + DEBUGGER_LOG_LEVEL.length (), param.length () - DEBUGGER_LOG_LEVEL.length ());
			set_debugger_log_level (level.get ());
		}
#endif
	}

	if ((log_categories & LOG_GC) != 0)
		_gc_spew_enabled = true;
}

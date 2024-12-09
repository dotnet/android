#include <array>
#include <cerrno>
#include <cstdarg>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <string>

#include <strings.h>
#include <unistd.h>

#include <android/log.h>

#include <constants.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/logger.hh>
#include <runtime-base/util.hh>
#include <shared/cpp-util.hh>
#include <shared/log_level.hh>

using namespace xamarin::android;
using std::operator""sv;

namespace {
	FILE*
	open_file (LogCategories category, std::string const& custom_path, std::string_view const& override_dir, std::string_view const& filename)
	{
		bool ignore_path = false;
		if (!custom_path.empty () && access (custom_path.c_str (), W_OK) < 0) {
			log_warn (category,
				"Could not open path '{}' for logging (\"{}\"). Using '{}/{}' instead.",
				custom_path,
				strerror (errno),
				override_dir,
				filename
			);
			ignore_path = true;
		}

		std::string p{};
		if (custom_path.empty () || ignore_path) {
			Util::create_public_directory (override_dir);
			p.assign (override_dir);
			p.append ("/");
			p.append (filename);
		}

		std::string const& path = p.empty () ? custom_path : p;
		unlink (path.c_str ());

		FILE *f = Util::monodroid_fopen (path, "a"sv);

		if (f) {
			Util::set_world_accessable (path);
		} else {
			log_warn (category, "Could not open path '{}' for logging: {}", path, strerror (errno));
		}

		return f;
	}

	std::string gref_file{};
	std::string lref_file{};
	bool light_gref  = false;
	bool light_lref  = false;
}

void
Logger::init_reference_logging (std::string_view const& override_dir) noexcept
{
	if ((log_categories & LOG_GREF) != 0 && !light_gref) {
		_gref_log  = open_file (LOG_GREF, gref_file, override_dir, "grefs.txt"sv);
	}

	if ((log_categories & LOG_LREF) != 0 && !light_lref) {
		// if both lref & gref have files specified, and they're the same path, reuse the FILE*.
		if (!lref_file.empty () && strcmp (lref_file.c_str (), !gref_file.empty () ? gref_file.c_str () : "") == 0) {
			_lref_log = _gref_log;
		} else {
			_lref_log = open_file (LOG_LREF, lref_file, override_dir, "lrefs.txt"sv);
		}
	}
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
Logger::init_logging_categories () noexcept
{
	_log_timing_categories = LogTimingCategories::Default;

	dynamic_local_string<Constants::PROPERTY_VALUE_BUFFER_LEN> value;
	if (AndroidSystem::monodroid_get_system_property (Constants::DEBUG_MONO_LOG_PROPERTY, value) == 0) {
		return;
	}

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

		auto get_log_file_name = [](std::string_view const& file_kind, string_segment const& segment, size_t offset) -> const char* {
			auto file_name = segment.at (offset);

			if (!file_name.has_value ()) {
				log_warn (LOG_DEFAULT, "Unable to set path to {} log file: {}", file_kind, to_string (file_name.error ()));
				return nullptr;
			}

			return file_name.value ();
		};

		constexpr std::string_view CAT_GREF_EQUALS { "gref=" };
		if (set_category (CAT_GREF_EQUALS, param, LOG_GREF, true /* arg_starts_with_name */)) {
			gref_file = get_log_file_name ("gref"sv, param, CAT_GREF_EQUALS.length ());
			continue;
		}

		if (set_category ("gref-", param, LOG_GREF)) {
			light_gref = true;
			continue;
		}

		if (set_category ("gref+", param, LOG_GREF)) {
			_gref_to_logcat = true;
			continue;
		}

		constexpr std::string_view CAT_LREF_EQUALS { "lref=" };
		if (set_category (CAT_LREF_EQUALS, param, LOG_LREF, true /* arg_starts_with_name */)) {
			lref_file = get_log_file_name ("lref"sv, param, CAT_LREF_EQUALS.length ());
			continue;
		}

		if (set_category ("lref-", param, LOG_LREF)) {
			light_lref = true;
			continue;
		}

		if (set_category ("lref+", param, LOG_LREF)) {
			_lref_to_logcat = true;
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
	}

	if ((log_categories & LOG_GC) != 0) {
		_gc_spew_enabled = true;
	}
}

#include <array>
#include <cerrno>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <limits>

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
	char *gref_file = nullptr;
	char *lref_file = nullptr;
	bool light_gref  = false;
	bool light_lref  = false;

	void set_log_file (char *&log_file, const char *path) noexcept
	{
		std::free (log_file);
		log_file = path == nullptr ? nullptr : strdup (path);
		abort_unless (path == nullptr || log_file != nullptr, "Failed to allocate reference log file path");
	}
}

[[gnu::always_inline]]
auto Logger::open_file (std::string_view const& path) noexcept -> FILE*
{
	if (path.empty ()) {
		return nullptr;
	}

	// Ignore errors, by design
	unlink (path.data ());

	// `monodroid_fopen` will log any errors
	FILE *ret = Util::monodroid_fopen (path, "a"sv);
	if (ret != nullptr) {
		Util::set_world_accessable (path);
	}

	return ret;
}

[[gnu::flatten, gnu::always_inline]]
auto Logger::open_file (LogCategories category, std::string_view const& custom_path, std::string_view const& override_dir, std::string_view const& fallback_filename) noexcept -> FILE*
{
	auto log_and_return = [&category](FILE *f, std::string_view const& path) -> FILE* {
		if (f != nullptr) {
			if ((log_categories & category) != 0) {
				char message[512];
				snprintf (message, sizeof (message), "Opened file '%s' for logging.", path.data ());
				log_write (category, LogLevel::Debug, message);
			}
		}
		return f;
	};

	FILE *ret = open_file (custom_path);
	if (ret != nullptr) {
		return log_and_return (ret, custom_path);
	}

	Util::create_public_directory (override_dir);
	dynamic_local_string<Constants::SENSIBLE_PATH_MAX> p;
	p.append (override_dir)
		.append ("/")
		.append (fallback_filename);

	return log_and_return (open_file (p.get ()), p.get ());
}

void
Logger::init_reference_logging (std::string_view const& override_dir) noexcept
{
	if ((log_categories & LOG_GREF) != 0 && !light_gref) {
		_gref_log  = open_file (LOG_GREF, gref_file == nullptr ? std::string_view {} : std::string_view { gref_file }, override_dir, "grefs.txt"sv);
	}

	if ((log_categories & LOG_LREF) != 0 && !light_lref) {
		// if both lref & gref have files specified, and they're the same path, reuse the FILE*.
		if (lref_file != nullptr && strcmp (lref_file, gref_file != nullptr ? gref_file : "") == 0) {
			_lref_log = _gref_log;
		} else {
			_lref_log = open_file (LOG_LREF, lref_file == nullptr ? std::string_view {} : std::string_view { lref_file }, override_dir, "lrefs.txt"sv);
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

	dynamic_local_property_string value;
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
				char message[256];
				std::string_view error = to_string (file_name.error ());
				snprintf (
					message,
					sizeof (message),
					"Unable to set path to %.*s log file: %.*s",
					static_cast<int>(file_kind.length ()),
					file_kind.data (),
					static_cast<int>(error.length ()),
					error.data ()
				);
				log_write (LOG_DEFAULT, LogLevel::Warn, message);
				return nullptr;
			}

			return file_name.value ();
		};

		constexpr std::string_view CAT_GREF_EQUALS { "gref=" };
		if (set_category (CAT_GREF_EQUALS, param, LOG_GREF, true /* arg_starts_with_name */)) {
			set_log_file (gref_file, get_log_file_name ("gref"sv, param, CAT_GREF_EQUALS.length ()));
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
			set_log_file (lref_file, get_log_file_name ("lref"sv, param, CAT_LREF_EQUALS.length ()));
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

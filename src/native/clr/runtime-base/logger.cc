#include <cstdarg>
#include <cstdlib>
#include <cstring>
#include <limits>

#include <strings.h>

#include <android/log.h>

#include <constants.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/logger.hh>
#include <runtime-base/util.hh>
#include <shared/cpp-util.hh>
#include <shared/log_level.hh>

using namespace xamarin::android;

namespace {
	char *gref_file = nullptr;
	char *lref_file = nullptr;
	const char *reference_log_dir = nullptr;
	bool light_gref  = false;
	bool light_lref  = false;

	// Compares a comma-separated parameter, which is not NUL-terminated, against `text`.
	bool param_matches (const char *param, size_t param_length, const char *text, bool prefix_only = false) noexcept
	{
		size_t text_length = strlen (text);
		if (prefix_only ? param_length < text_length : param_length != text_length) {
			return false;
		}

		return strncmp (param, text, text_length) == 0;
	}

	void set_log_file (char *&log_file, const char *path, size_t path_length) noexcept
	{
		char *new_log_file = nullptr;
		if (path != nullptr && path_length > 0) {
			size_t allocation_size = Helpers::add_with_overflow_check<size_t> (path_length, 1uz);
			new_log_file = static_cast<char*> (std::malloc (allocation_size));
			abort_unless (new_log_file != nullptr, "Failed to allocate reference log file path");

			memcpy (new_log_file, path, path_length);
			new_log_file [path_length] = '\0';
		}

		std::free (log_file);
		log_file = new_log_file;
	}
}

void
Logger::init_reference_logging (const char *override_dir) noexcept
{
	reference_log_dir = override_dir;
}

auto Logger::gref_log_path () noexcept -> const char*
{
	return gref_file;
}

auto Logger::lref_log_path () noexcept -> const char*
{
	return lref_file;
}

auto Logger::reference_log_directory () noexcept -> const char*
{
	return reference_log_dir;
}

auto Logger::light_gref_enabled () noexcept -> bool
{
	return light_gref;
}

auto Logger::light_lref_enabled () noexcept -> bool
{
	return light_lref;
}

auto Logger::gref_enabled () noexcept -> bool
{
	return (log_categories & LOG_GREF) != 0;
}

[[gnu::always_inline]] bool
Logger::set_category (const char *name, const char *arg, size_t arg_length, unsigned int entry, bool arg_starts_with_name) noexcept
{
	if ((log_categories & entry) == entry) {
		return false;
	}

	if (param_matches (arg, arg_length, name, arg_starts_with_name)) {
		log_categories |= entry;
		return true;
	}

	return false;
}

void
Logger::init_logging_categories () noexcept
{
	_log_timing_categories = LogTimingCategories::Default;

	char value[Constants::PROPERTY_VALUE_BUFFER_LEN];
	const char *categories = AndroidSystem::monodroid_get_system_property (Constants::DEBUG_DOTNET_LOG_PROPERTY.data (), value, sizeof (value));
	if (categories == nullptr) {
		categories = AndroidSystem::monodroid_get_system_property (Constants::LEGACY_DEBUG_MONO_LOG_PROPERTY.data (), value, sizeof (value));
	}
	if (categories == nullptr) {
		return;
	}

	// The value may point at immortal bundled property data, so the parameters cannot be
	// NUL-terminated in place. Bound every comparison by `param_length` instead.
	const char *param = categories;
	while (param != nullptr && *param != '\0') {
		const char *separator = strchr (param, ',');
		size_t param_length = separator != nullptr ? static_cast<size_t>(separator - param) : strlen (param);
		const char *next = separator == nullptr ? nullptr : separator + 1;

		if (param_matches (param, param_length, "all")) {
			log_categories = 0xFFFFFFFF;
			break;
		}

		if (set_category ("assembly", param, param_length, LOG_ASSEMBLY) ||
		    set_category ("default",  param, param_length, LOG_DEFAULT) ||
		    set_category ("debugger", param, param_length, LOG_DEBUGGER) ||
		    set_category ("gc",       param, param_length, LOG_GC) ||
		    set_category ("gref",     param, param_length, LOG_GREF) ||
		    set_category ("lref",     param, param_length, LOG_LREF) ||
		    set_category ("timing",   param, param_length, LOG_TIMING) ||
		    set_category ("network",  param, param_length, LOG_NET) ||
		    set_category ("netlink",  param, param_length, LOG_NETLINK)) {
			param = next;
			continue;
		}

		auto set_log_file_from_param = [param, param_length](char *&log_file, const char *file_kind) {
			constexpr size_t OFFSET = sizeof ("gref=") - 1; // Both the "gref=" and "lref=" prefixes are this long.
			if (OFFSET >= param_length) {
				log_warnf (LOG_DEFAULT, "Unable to set path to %s log file: no file name specified", file_kind);
				set_log_file (log_file, nullptr, 0uz);
				return;
			}

			set_log_file (log_file, param + OFFSET, param_length - OFFSET);
		};

		if (set_category ("gref=", param, param_length, LOG_GREF, true /* arg_starts_with_name */)) {
			set_log_file_from_param (gref_file, "gref");
			param = next;
			continue;
		}

		if (set_category ("gref-", param, param_length, LOG_GREF)) {
			light_gref = true;
			param = next;
			continue;
		}

		if (set_category ("gref+", param, param_length, LOG_GREF)) {
			_gref_to_logcat = true;
			param = next;
			continue;
		}

		if (set_category ("lref=", param, param_length, LOG_LREF, true /* arg_starts_with_name */)) {
			set_log_file_from_param (lref_file, "lref");
			param = next;
			continue;
		}

		if (set_category ("lref-", param, param_length, LOG_LREF)) {
			light_lref = true;
			param = next;
			continue;
		}

		if (set_category ("lref+", param, param_length, LOG_LREF)) {
			_lref_to_logcat = true;
			param = next;
			continue;
		}

		if (param_matches (param, param_length, "timing=fast-bare", true /* prefix_only */)) {
			log_categories |= LOG_TIMING;
			_log_timing_categories |= LogTimingCategories::FastBare;
			param = next;
			continue;
		}

		if (param_matches (param, param_length, "timing=bare", true /* prefix_only */)) {
			log_categories |= LOG_TIMING;
			_log_timing_categories |= LogTimingCategories::Bare;
		}

		param = next;
	}

	if ((log_categories & LOG_GC) != 0) {
		_gc_spew_enabled = true;
	}
}

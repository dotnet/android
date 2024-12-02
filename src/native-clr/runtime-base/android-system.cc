#include <limits>

#include <constants.hh>
#include <xamarin-app.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/strings.hh>

using namespace xamarin::android;

auto
AndroidSystem::lookup_system_property (std::string_view const& name, size_t &value_len) noexcept -> const char*
{
	value_len = 0;
#if defined (DEBUG)
	BundledProperty *p = lookup_system_property (name);
	if (p != nullptr) {
		value_len = p->value_len;
		return p->name;
	}
#endif // DEBUG || !ANDROID

	if (application_config.system_property_count == 0) {
		return nullptr;
	}

	if (application_config.system_property_count % 2 != 0) {
		log_warn (LOG_DEFAULT, "Corrupted environment variable array: does not contain an even number of entries ({})", application_config.system_property_count);
		return nullptr;
	}

	const char *prop_name;
	const char *prop_value;
	for (size_t i = 0uz; i < application_config.system_property_count; i += 2uz) {
		prop_name = app_system_properties[i];
		if (prop_name == nullptr || *prop_name == '\0') {
			continue;
		}

		if (strcmp (prop_name, name.data ()) == 0) {
			prop_value = app_system_properties [i + 1uz];
			if (prop_value == nullptr || *prop_value == '\0') {
				value_len = 0uz;
				return "";
			}

			value_len = strlen (prop_value);
			return prop_value;
		}
	}

	return nullptr;
}

auto
AndroidSystem::monodroid__system_property_get (std::string_view const& name, char *sp_value, size_t sp_value_len) noexcept -> int
{
	if (name.empty () || sp_value == nullptr) {
		return -1;
	}

	char *buf = nullptr;
	if (sp_value_len < Constants::PROPERTY_VALUE_BUFFER_LEN) {
		size_t alloc_size = Helpers::add_with_overflow_check<size_t> (Constants::PROPERTY_VALUE_BUFFER_LEN, 1uz);
		log_warn (LOG_DEFAULT, "Buffer to store system property may be too small, will copy only {} bytes", sp_value_len);
		buf = new char [alloc_size];
	}

	int len = __system_property_get (name.data (), buf ? buf : sp_value);
	if (buf != nullptr) {
		strncpy (sp_value, buf, sp_value_len);
		sp_value [sp_value_len] = '\0';
		delete[] buf;
	}

	return len;
}

auto AndroidSystem::monodroid_get_system_property (std::string_view const& name, dynamic_local_string<Constants::PROPERTY_VALUE_BUFFER_LEN> &value) noexcept -> int
{
	int len = monodroid__system_property_get (name, value.get (), value.size ());
	if (len > 0) {
		// Clumsy, but if we want direct writes to be fast, this is the price we pay
		value.set_length_after_direct_write (static_cast<size_t>(len));
		return len;
	}

	size_t plen;
	const char *v = lookup_system_property (name, plen);
	if (v == nullptr)
		return len;

	value.assign (v, plen);
	return Helpers::add_with_overflow_check<int> (plen, 0);
}

auto
AndroidSystem::get_max_gref_count_from_system () noexcept -> long
{
	long max;

	if (running_in_emulator) {
		max = 2000;
	} else {
		max = 51200;
	}

	dynamic_local_string<Constants::PROPERTY_VALUE_BUFFER_LEN> override;
	if (monodroid_get_system_property (Constants::DEBUG_MONO_MAX_GREFC, override) > 0) {
		char *e;
		max       = strtol (override.get (), &e, 10);
		switch (*e) {
			case 'k':
				e++;
				max *= 1000;
				break;
			case 'm':
				e++;
				max *= 1000000;
				break;
		}

		if (max < 0) {
			max = std::numeric_limits<int>::max ();
		}

		if (*e) {
			log_warn (LOG_GC, "Unsupported '{}' value '{}'.", Constants::DEBUG_MONO_MAX_GREFC.data (), override.get ());
		}

		log_warn (LOG_GC, "Overriding max JNI Global Reference count to {}", max);
	}

	return max;
}

#include <limits>
#include <string_view>

#include <runtime-base/android-system.hh>

using namespace xamarin::android;

using std::operator""sv;

auto AndroidSystem::monodroid_get_system_property (std::string_view const& name, char *value, size_t value_size) noexcept -> int
{
	if (value == nullptr || value_size == 0) {
		return -1;
	}

	value [0] = '\0';
	if (value_size < Constants::PROPERTY_VALUE_BUFFER_LEN) {
		return -1;
	}

	int len = monodroid__system_property_get (name, value, value_size);
	if (len > 0) {
		return len;
	}

	size_t property_length;
	const char *property_value = lookup_system_property (name, property_length);
	if (property_value == nullptr) {
		return len;
	}

	if (property_length >= value_size) {
		return -1;
	}

	memcpy (value, property_value, property_length);
	value [property_length] = '\0';
	return Helpers::add_with_overflow_check<int> (property_length, 0);
}

auto
AndroidSystem::monodroid__system_property_get (std::string_view const& name, char *sp_value, size_t sp_value_len) noexcept -> int
{
	if (name.empty () || sp_value == nullptr) {
		return -1;
	}

	// `__system_property_get` always writes up to `PROP_VALUE_MAX` bytes and cannot be told about
	// a smaller buffer, so callers must provide at least `PROPERTY_VALUE_BUFFER_LEN` bytes.
	if (sp_value_len < Constants::PROPERTY_VALUE_BUFFER_LEN) {
		log_warnf (
			LOG_DEFAULT,
			"Buffer to store system property '%.*s' is too small (%zu bytes)",
			static_cast<int>(name.length ()),
			name.data (),
			sp_value_len
		);
		return -1;
	}

	// `__system_property_get` takes a C string, so `name` must be NUL-terminated. All callers pass
	// views over string literals, which satisfy that.
	return __system_property_get (name.data (), sp_value);
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

	char override[Constants::PROPERTY_VALUE_BUFFER_LEN];
	if (monodroid_get_system_property (Constants::DEBUG_MONO_MAX_GREFC, override, sizeof (override)) > 0) {
		char *e;
		max = strtol (override, &e, 10);
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
			log_warnf (
				LOG_GC,
				"Unsupported '%.*s' value '%s'.",
				static_cast<int>(Constants::DEBUG_MONO_MAX_GREFC.length ()),
				Constants::DEBUG_MONO_MAX_GREFC.data (),
				override
			);
		}

		log_warnf (LOG_GC, "Overriding max JNI Global Reference count to %ld", max);
	}

	return max;
}

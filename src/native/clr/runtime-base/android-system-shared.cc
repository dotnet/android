#include <limits>

#include <runtime-base/android-system.hh>

using namespace xamarin::android;

auto AndroidSystem::monodroid_get_system_property (const char *name, char *value, size_t value_size) noexcept -> const char*
{
	// `__system_property_get` always writes up to `PROPERTY_VALUE_BUFFER_LEN` bytes, so a smaller
	// buffer would overflow. This is a programming error, not a runtime condition.
	abort_unless (
		value != nullptr && value_size >= Constants::PROPERTY_VALUE_BUFFER_LEN,
		"System property value buffer is too small"
	);

	value [0] = '\0';

	// `__system_property_get` NUL-terminates what it writes.
	if (monodroid__system_property_get (name, value) > 0) {
		return value;
	}

	// Bundled properties are NUL-terminated strings owned by the application, so return them
	// directly rather than copying them into `value`. Their length is therefore not limited by
	// `Constants::PROPERTY_VALUE_BUFFER_LEN`. See the header for the exact lifetime guarantee.
	//
	// A bundled property may be present but empty. `__system_property_get` cannot distinguish an
	// empty value from a missing one either, so report both as unset and let callers keep their
	// defaults.
	size_t property_length;
	const char *bundled_value = lookup_system_property (name, property_length);
	if (bundled_value == nullptr || property_length == 0) {
		return nullptr;
	}

	return bundled_value;
}

auto
AndroidSystem::monodroid__system_property_get (const char *name, char *sp_value) noexcept -> int
{
	if (name == nullptr || *name == '\0' || sp_value == nullptr) {
		return -1;
	}

	// The caller guarantees that `sp_value` is at least `PROPERTY_VALUE_BUFFER_LEN` bytes long.
	return __system_property_get (name, sp_value);
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
	std::string_view property_name = Constants::DEBUG_DOTNET_MAX_GREFC;
	const char *grefc = monodroid_get_system_property (property_name.data (), override, sizeof (override));
	if (grefc == nullptr) {
		property_name = Constants::LEGACY_DEBUG_MONO_MAX_GREFC;
		grefc = monodroid_get_system_property (property_name.data (), override, sizeof (override));
	}
	if (grefc != nullptr) {
		char *e;
		max = strtol (grefc, &e, 10);
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
				"Unsupported '%s' value '%s'.",
				property_name.data (),
				grefc
			);
		}

		log_warnf (LOG_GC, "Overriding max JNI Global Reference count to %ld", max);
	}

	return max;
}

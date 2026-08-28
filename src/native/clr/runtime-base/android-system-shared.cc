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

	// Bundled properties are NUL-terminated strings in static application data which live as long
	// as the process, so return them directly rather than copying them into `value`. Their length
	// is therefore not limited by `Constants::PROPERTY_VALUE_BUFFER_LEN`.
	size_t property_length;
	return lookup_system_property (name, property_length);
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
	const char *grefc = monodroid_get_system_property (Constants::DEBUG_MONO_MAX_GREFC.data (), override, sizeof (override));
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
				Constants::DEBUG_MONO_MAX_GREFC.data (),
				grefc
			);
		}

		log_warnf (LOG_GC, "Overriding max JNI Global Reference count to %ld", max);
	}

	return max;
}

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

	char *buf = nullptr;
	if (sp_value_len < Constants::PROPERTY_VALUE_BUFFER_LEN) {
		size_t alloc_size = Helpers::add_with_overflow_check<size_t> (Constants::PROPERTY_VALUE_BUFFER_LEN, 1uz);
		log_warnf (LOG_DEFAULT, "Buffer to store system property may be too small, will copy only %zu bytes", sp_value_len);
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

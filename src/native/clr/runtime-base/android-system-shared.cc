#include <limits>
#include <string_view>

#include <runtime-base/android-system.hh>

using namespace xamarin::android;

using std::operator""sv;

auto AndroidSystem::monodroid_get_system_property (std::string_view const& name, dynamic_local_property_string &value) noexcept -> int
{
	int len = monodroid__system_property_get (name, value.get (), value.size ());
	if (len > 0) {
		// Clumsy, but if we want direct writes to be fast, this is the price we pay
		value.set_length_after_direct_write (static_cast<size_t>(len));
		return len;
	}

	size_t plen;
	const char *v = lookup_system_property (name, plen);
	if (v == nullptr) {
		return len;
	}

	value.assign (v, plen);
	return Helpers::add_with_overflow_check<int> (plen, 0);
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

auto
AndroidSystem::get_max_gref_count_from_system () noexcept -> long
{
	long max;

	if (running_in_emulator) {
		max = 2000;
	} else {
		max = 51200;
	}

	dynamic_local_property_string override;
	if (monodroid_get_system_property (Constants::DEBUG_MONO_MAX_GREFC, override) > 0) {
		char *e;
		max = strtol (override.get (), &e, 10);
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

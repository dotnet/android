#include <cstring>
#include <string_view>

#include <host/host-environment.hh>
#include <runtime-base/android-system.hh>

using namespace xamarin::android;

auto
AndroidSystem::lookup_system_property (std::string_view const& name, [[maybe_unused]] size_t &value_len) noexcept -> const char*
{
	if (__naot_android_app_system_property_count == 0) {
		return nullptr;
	}

	for (size_t i = 0; i < __naot_android_app_system_property_count; i++) {
		AppEnvironmentVariable const& sys_prop = __naot_android_app_system_properties[i];
		const char *prop_name = &__naot_android_app_system_property_contents[sys_prop.name_index];
		if (name.compare (prop_name) != 0) {
			continue;
		}

		const char *prop_value = &__naot_android_app_system_property_contents[sys_prop.value_index];
		value_len = strlen (prop_value);
		return prop_value;
	}

	return nullptr;
}

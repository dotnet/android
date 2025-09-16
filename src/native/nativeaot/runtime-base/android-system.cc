#include <cstring>
#include <string_view>

#include <host/host-environment-naot.hh>
#include <runtime-base/android-system.hh>

using namespace xamarin::android;

auto AndroidSystem::lookup_system_property (std::string_view const& name, size_t &value_len) noexcept -> const char*
{
	return HostEnvironment::lookup_system_property (
		name,
		value_len,
		__naot_android_app_system_property_count,
		__naot_android_app_system_properties,
		__naot_android_app_system_property_contents
	);
}

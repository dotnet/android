#include <cstring>
#include <string_view>

#include <runtime-base/android-system.hh>
#include <runtime-base/app-system-properties.hh>

using namespace xamarin::android;

auto AndroidSystem::lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*
{
	return AppSystemProperties::lookup (name, value_len);
}

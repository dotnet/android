#include <runtime-base/app-bootstrap-properties.hh>
#include <runtime-base/android-system.hh>

using namespace xamarin::android;

auto AndroidSystem::lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*
{
	return AppBootstrapProperties::lookup (name, value_len);
}

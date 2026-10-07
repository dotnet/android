#include <cstdint>

#include <host/host-environment-naot.hh>
#include <runtime-base/app-system-properties.hh>

using namespace xamarin::android;

void HostEnvironment::init () noexcept
{
	for (AppSystemProperties::Entry const& property : AppSystemProperties::entries ()) {
		set_system_property (property.name, property.value);
	}
}

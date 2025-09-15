#include <cstdint>

#include <host/host-environment.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

extern "C" {
	extern const uint32_t __naot_android_app_environment_variable_count;
	extern const AppEnvironmentVariable __naot_android_app_environment_variables[];
	extern const char __naot_android_app_environment_variable_contents[];
}

void HostEnvironment::init () noexcept
{
	if (__naot_android_app_environment_variable_count > 0) {
		log_debug (LOG_DEFAULT, "Setting environment variables ({})", __naot_android_app_environment_variable_count);
		set_values<set_variable> (
			__naot_android_app_environment_variable_count,
			__naot_android_app_environment_variables,
			__naot_android_app_environment_variable_contents
		);
	}

	if (__naot_android_app_system_property_count == 0) {
		return;
	}

	log_debug (LOG_DEFAULT, "Setting system properties ({})", __naot_android_app_system_property_count);
	set_values<set_system_property> (
		__naot_android_app_system_property_count,
		__naot_android_app_system_properties,
		__naot_android_app_system_property_contents
	);
}

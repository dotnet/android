#include <cstdint>

#include <host/host-environment.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

struct AppEnvironmentVariable {
	uint32_t name_index;
	uint32_t value_index;
};

extern "C" {
	extern const uint32_t __naot_android_app_environment_variable_count;
	extern const AppEnvironmentVariable __naot_android_app_environment_variables[];
	extern const char __naot_android_app_environment_variable_contents[];
}

void HostEnvironment::init () noexcept
{
	if (__naot_android_app_environment_variable_count == 0) {
		return;
	}

	log_debug (LOG_DEFAULT, "Setting {} environment variables", __naot_android_app_environment_variable_count);
	for (size_t i = 0; i < __naot_android_app_environment_variable_count; i++) {
		AppEnvironmentVariable const& env_var = __naot_android_app_environment_variables[i];
		const char *var_name = &__naot_android_app_environment_variable_contents[env_var.name_index];
		const char *var_value = &__naot_android_app_environment_variable_contents[env_var.value_index];

		set_variable (var_name, var_value);
	}
}

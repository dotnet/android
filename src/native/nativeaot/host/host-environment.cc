#include <cstdint>

#include <host/host-environment-naot.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

<<<<<<< HEAD
=======
struct AppEnvironmentVariable {
	uint32_t name_index;
	uint32_t value_index;
};

>>>>>>> main
extern "C" {
	extern const uint32_t __naot_android_app_environment_variable_count;
	extern const AppEnvironmentVariable __naot_android_app_environment_variables[];
	extern const char __naot_android_app_environment_variable_contents[];
<<<<<<< HEAD

	extern const uint32_t __naot_android_app_system_property_count;
	extern const AppEnvironmentVariable __naot_android_app_system_properties[];
	extern const char __naot_android_app_system_property_contents[];
=======
>>>>>>> main
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

<<<<<<< HEAD
	log_debug (LOG_DEFAULT, "Setting environment variables ({})", __naot_android_app_environment_variable_count);
	set_values<set_variable> (
		__naot_android_app_environment_variable_count,
		__naot_android_app_environment_variables,
		__naot_android_app_environment_variable_contents
	);

	log_debug (LOG_DEFAULT, "Setting system properties ({})", __naot_android_app_system_property_count);
	set_values<set_system_property> (
		__naot_android_app_system_property_count,
		__naot_android_app_system_properties,
		__naot_android_app_system_property_contents
	);
=======
	log_debug (LOG_DEFAULT, "Setting {} environment variables", __naot_android_app_environment_variable_count);
	for (size_t i = 0; i < __naot_android_app_environment_variable_count; i++) {
		AppEnvironmentVariable const& env_var = __naot_android_app_environment_variables[i];
		const char *var_name = &__naot_android_app_environment_variable_contents[env_var.name_index];
		const char *var_value = &__naot_android_app_environment_variable_contents[env_var.value_index];

		set_variable (var_name, var_value);
	}
>>>>>>> main
}

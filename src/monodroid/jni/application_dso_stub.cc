#include <stdint.h>
#include <stdlib.h>

#include "xamarin-app.h"

// This file MUST have valid values everywhere - the DSO it is compiled into is loaded by the
// designer on desktop.

TypeMapHeader jm_typemap_header = { 1, 0, 0, 0 };
uint8_t jm_typemap[] = { 0 };

TypeMapHeader mj_typemap_header = { 1, 0, 0, 0 };
uint8_t mj_typemap[] = { 0 };

ApplicationConfig application_config = {
	.uses_mono_llvm = false,
	.uses_mono_aot = false,
	.uses_assembly_preload = false,
	.is_a_bundled_app = false,
	.broken_exception_transitions = false,
	.package_naming_policy = 0,
	.environment_variable_count = 0,
	.system_property_count = 0,
	.android_package_name = "com.xamarin.test",
};

const char* mono_aot_mode_name = "";
const char* app_environment_variables[] = {};
const char* app_system_properties[] = {};

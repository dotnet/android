#include <stdint.h>
#include <stdlib.h>

#include "xamarin-app.hh"

// This file MUST have "valid" values everywhere - the DSO it is compiled into is loaded by the
// designer on desktop.
const uint32_t map_module_count = 0;
const uint32_t java_type_count = 0;
const uint32_t java_name_width = 0;

const TypeMapModule map_modules[] = {};
const TypeMapJava map_java[] = {};

ApplicationConfig application_config = {
	.uses_mono_llvm = false,
	.uses_mono_aot = false,
	.uses_assembly_preload = false,
	.is_a_bundled_app = false,
	.broken_exception_transitions = false,
	.bound_exception_type = 0, // System
	.package_naming_policy = 0,
	.environment_variable_count = 0,
	.system_property_count = 0,
	.android_package_name = "com.xamarin.test",
};

const char* mono_aot_mode_name = "";
const char* app_environment_variables[] = {};
const char* app_system_properties[] = {};

#include <stdint.h>
#include <stdlib.h>

#include "xamarin-app.hh"

// This file MUST have "valid" values everywhere - the DSO it is compiled into is loaded by the
// designer on desktop.
uint64_t format_tag = FORMAT_TAG;

#if defined (DEBUG) || !defined (ANDROID)
static TypeMapEntry java_to_managed[] = {};

static TypeMapEntry managed_to_java[] = {};

// MUST match src/Xamarin.Android.Build.Tasks/Utilities/TypeMappingDebugNativeAssemblyGenerator.cs
const TypeMap type_map = {
	0,
	nullptr,
	nullptr,
	java_to_managed,
	managed_to_java
};
#else
const uint32_t map_module_count = 0;
const uint32_t java_type_count = 0;
const uint32_t java_name_width = 0;

const TypeMapModule map_modules[] = {};
const TypeMapJava map_java[] = {};
#endif

CompressedAssemblies compressed_assemblies = {
	/*.count = */ 0,
	/*.descriptors = */ nullptr,
};

ApplicationConfig application_config = {
	/*.uses_mono_llvm =*/ false,
	/*.uses_mono_aot =*/ false,
	/*.uses_assembly_preload =*/ false,
	/*.is_a_bundled_app =*/ false,
	/*.broken_exception_transitions =*/ false,
	/*.instant_run_enabled =*/ false,
	/*.jni_add_native_method_registration_attribute_present =*/ false,
	/*.have_runtime_config_blob =*/ false,
	/*.bound_exception_type =*/ 0, // System
	/*.package_naming_policy =*/ 0,
	/*.environment_variable_count =*/ 0,
	/*.system_property_count =*/ 0,
	/*.android_package_name =*/ "com.xamarin.test",
};

const char* mono_aot_mode_name = "";
const char* app_environment_variables[] = {};
const char* app_system_properties[] = {};

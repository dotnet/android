#include <stdint.h>
#include <stdlib.h>

#include "xamarin-app.hh"
#include "xxhash.hh"

// This file MUST have "valid" values everywhere - the DSO it is compiled into is loaded by the
// designer on desktop.
const uint64_t format_tag = FORMAT_TAG;

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

//
// Config settings below **must** be valid for Desktop builds as the default `libxamarin-app.{dll,dylib,so}` is used by
// the Designer
//
constexpr char android_package_name[] = "com.xamarin.test";
const ApplicationConfig application_config = {
	.uses_mono_llvm = false,
	.uses_mono_aot = false,
	.uses_assembly_preload = false,
	.is_a_bundled_app = false,
	.broken_exception_transitions = false,
	.instant_run_enabled = false,
	.jni_add_native_method_registration_attribute_present = false,
	.have_runtime_config_blob = false,
	.have_assembly_store = false,
	.bound_exception_type = 0, // System
	.package_naming_policy = 0,
	.environment_variable_count = 0,
	.system_property_count = 0,
	.number_of_assemblies_in_apk = 2,
	.bundled_assembly_name_width = 0,
	.number_of_assembly_store_files = 2,
	.number_of_dso_cache_entries = 2,
	.mono_components_mask = MonoComponent::None,
	.android_package_name = android_package_name,
};

const char* const mono_aot_mode_name = "normal";
const char* const app_environment_variables[] = {};
const char* const app_system_properties[] = {};

static constexpr size_t AssemblyNameWidth = 128;

static char first_assembly_name[AssemblyNameWidth];
static char second_assembly_name[AssemblyNameWidth];

XamarinAndroidBundledAssembly bundled_assemblies[] = {
	{
		.apk_fd = -1,
		.data_offset = 0,
		.data_size = 0,
		.data = nullptr,
		.name_length = 0,
		.name = first_assembly_name,
	},

	{
		.apk_fd = -1,
		.data_offset = 0,
		.data_size = 0,
		.data = nullptr,
		.name_length = 0,
		.name = second_assembly_name,
	},
};

AssemblyStoreSingleAssemblyRuntimeData assembly_store_bundled_assemblies[] = {
	{
		.image_data = nullptr,
		.debug_info_data = nullptr,
		.config_data = nullptr,
		.descriptor = nullptr,
	},

	{
		.image_data = nullptr,
		.debug_info_data = nullptr,
		.config_data = nullptr,
		.descriptor = nullptr,
	},
};

AssemblyStoreRuntimeData assembly_stores[] = {
	{
		.data_start = nullptr,
		.assembly_count = 0,
		.assemblies = nullptr,
	},

	{
		.data_start = nullptr,
		.assembly_count = 0,
		.assemblies = nullptr,
	},
};

constexpr char fake_dso_name[] = "libaot-Some.Assembly.dll.so";
constexpr char fake_dso_name2[] = "libaot-Another.Assembly.dll.so";

DSOCacheEntry dso_cache[] = {
	{
		.hash = xamarin::android::xxhash::hash (fake_dso_name, sizeof(fake_dso_name) - 1),
		.ignore = true,
		.name = fake_dso_name,
		.handle = nullptr,
	},

	{
		.hash = xamarin::android::xxhash::hash (fake_dso_name2, sizeof(fake_dso_name2) - 1),
		.ignore = true,
		.name = fake_dso_name2,
		.handle = nullptr,
	},
};

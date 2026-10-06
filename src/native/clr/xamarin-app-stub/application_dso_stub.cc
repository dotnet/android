#include <cstdint>
#include <stdlib.h>

#include <managed-interface.hh>
#include <xamarin-app.hh>

// This file MUST have "valid" values everywhere - the DSO it is compiled into is loaded by the
// designer on desktop.
const uint64_t format_tag = FORMAT_TAG;

//
// Config settings below **must** be valid for Desktop builds as the default `libxamarin-app.{dll,dylib,so}` is used by
// the Designer
//
constexpr char android_package_name[] = "com.xamarin.test";
const ApplicationConfig application_config = {
	.ignore_split_configs = false,
	.number_of_runtime_properties = 3,
	.package_naming_policy = 0,
	.environment_variable_count = 0,
	.system_property_count = 0,
	.number_of_assemblies_in_apk = 2,
	.bundled_assembly_name_width = 0,
	.number_of_dso_cache_entries = 2,
	.number_of_shared_libraries = 2,
	.android_package_name = android_package_name,
	.have_assembly_store = false,
};

// TODO: migrate to std::string_view for these two
const AppEnvironmentVariable app_environment_variables[] = {};
const char app_environment_variable_contents[] = {};
const AppEnvironmentVariable app_system_properties[] = {};
const char app_system_property_contents[] = {};
constexpr char fake_dso_name[] = "libSome.Library.so";
constexpr char fake_dso_name2[] = "libAnother.Library.so";

DSOCacheEntry dso_cache[] = {
	{
		.hash = xamarin::android::crc32_hash (fake_dso_name),
		.ignore = true,
		.is_jni_library = false,
		.name_index = 1,
		.handle = nullptr,
	},

	{
		.hash = xamarin::android::crc32_hash (fake_dso_name2),
		.ignore = true,
		.is_jni_library = false,
		.name_index = 2,
		.handle = nullptr,
	},
};

const uint dso_jni_preloads_idx_stride = 1;
const uint dso_jni_preloads_idx_count = 1;
const uint dso_jni_preloads_idx[1] = {
	0
};

const char dso_names_data[] = {};


const char *init_runtime_property_names[] = {
	"HOST_RUNTIME_CONTRACT",
	"RUNTIME_IDENTIFIER",
	"APP_CONTEXT_BASE_DIRECTORY",
};

char *init_runtime_property_values[] {
	nullptr,
	nullptr,
	nullptr,
};

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
const char* const java_type_names[] = {};

TypeMapModule map_modules[] = {};
const TypeMapJava map_java[] = {};
const xamarin::android::hash_t map_java_hashes[] = {};
#endif

//
// Config settings below **must** be valid for Desktop builds as the default `libxamarin-app.{dll,dylib,so}` is used by
// the Designer
//
constexpr char android_package_name[] = "com.xamarin.test";
const ApplicationConfig application_config = {
	.uses_mono_llvm = false,
	.uses_mono_aot = false,
	.aot_lazy_load = false,
	.uses_assembly_preload = false,
	.broken_exception_transitions = false,
	.instant_run_enabled = false,
	.jni_add_native_method_registration_attribute_present = false,
	.have_runtime_config_blob = false,
	.have_standalone_assembly_dsos = false,
	.marshal_methods_enabled = false,
	.bound_exception_type = 0, // System
	.package_naming_policy = 0,
	.environment_variable_count = 0,
	.system_property_count = 0,
	.number_of_assemblies_in_apk = 2,
	.bundled_assembly_name_width = 0,
	.number_of_dso_cache_entries = 2,
	.android_runtime_jnienv_class_token = 1,
	.jnienv_initialize_method_token = 2,
	.jnienv_registerjninatives_method_token = 3,
	.jni_remapping_replacement_type_count = 2,
	.jni_remapping_replacement_method_index_entry_count = 2,
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

//
// Support for marshal methods
//
#if defined (RELEASE) && defined (ANDROID) && defined (NET)
MonoImage* assembly_image_cache[] = {
	nullptr,
	nullptr,

};

// Each element contains an index into `assembly_image_cache`
const uint32_t assembly_image_cache_indices[] = {
	0,
	1,
	1,
	1,
};

// hashes point to indices in `assembly_image_cache_indices`
const xamarin::android::hash_t assembly_image_cache_hashes[] = {
	0,
	1,
	2,
	3,
};

uint32_t marshal_methods_number_of_classes = 2;
MarshalMethodsManagedClass marshal_methods_class_cache[] = {
	{
		.token = 0,
		.klass = nullptr,
	},

	{
		.token = 0,
		.klass = nullptr,
	},
};

const char* const mm_class_names[2] = {
	"one",
	"two",
};

const MarshalMethodName mm_method_names[] = {
	{
		.id = 1,
		.name = "one",
	},

	{
		.id = 2,
		.name = "two",
	},
};

alignas(4096) const uint8_t xa_input_assembly_data[InputAssemblyDataSize] = {
	0x04, 0x00, 0x30, 0x26, 0xfe, 0xfb, 0x37, 0xf4,  0xb7, 0x19, 0x0f, 0xdc, 0xad, 0xb5, 0x3c, 0x82,
	0xf4, 0xd9, 0x64, 0xe3, 0x56, 0x95, 0x7a, 0xef,  0x0b, 0x79, 0xbe, 0x28, 0x2b, 0x2a, 0x31, 0x54,
	0xf1, 0x2a, 0x76, 0xf9, 0x84, 0x5a, 0x5e, 0x0c,  0x11, 0x30, 0xaf, 0x5d, 0xb1, 0xff, 0x0f, 0x48,
};

alignas(4096) uint8_t xa_uncompressed_assembly_data[UncompressedAssemblyDataSize] = { };

const AssemblyEntry xa_assemblies[AssemblyCount] = {
	{
		.input_data_offset = 0,
		.input_data_size = 256,
		.uncompressed_data_offset = 0,
		.uncompressed_data_size = 0,
	},

	{
		.input_data_offset = 256,
		.input_data_size = 768,
		.uncompressed_data_offset = 0,
		.uncompressed_data_size = 2048,
	},
};

const AssemblyIndexEntry xa_assembly_index[AssemblyCount] = {
	{
		.name_hash = 11111u,
		.assemblies_index = 0,
		.has_extension = true,
		.is_standalone = false,
	},

	{
		.name_hash = 22222u,
		.assemblies_index = 1,
		.has_extension = true,
		.is_standalone = true,
	},
};

const char xa_assembly_names[AssemblyCount][AssemblyNameLength] = {
	"Assembly1.dll",
	"AnotherAssembly2.dll",
};

const char xa_assembly_dso_names[AssemblyCount][SharedLibraryNameLength] = {
	"libxaAssembly1.so",
	"libxaAnotherAssembly2.so",
};

const AssembliesConfig xa_assemblies_config = {
	.input_assembly_data_size = InputAssemblyDataSize,
	.uncompressed_assembly_data_size = UncompressedAssemblyDataSize,
	.assembly_name_length = AssemblyNameLength,
	.assembly_count = AssemblyCount,
	.assembly_dso_count = 2,
	.shared_library_name_length = SharedLibraryNameLength,
};

AssemblyLoadInfo xa_assemblies_load_info[AssemblyCount];

void xamarin_app_init ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] get_function_pointer_fn fn) noexcept
{
	// Dummy
}
#endif // def RELEASE && def ANDROID && def NET

static const JniRemappingIndexMethodEntry some_java_type_one_methods[] = {
	{
		.name = {
			.length = 15,
			.str = "old_method_name",
		},

		.signature = {
			.length = 0,
			.str = nullptr,
		},

		.replacement = {
			.target_type = "some/java/target_type_one",
			.target_name = "new_method_name",
			.is_static = false,
		}
	},
};

static const JniRemappingIndexMethodEntry some_java_type_two_methods[] = {
	{
		.name = {
			.length = 15,
			.str = "old_method_name",
		},

		.signature = {
			.length = 28,
			.str = "(IILandroid/content/Intent;)",
		},

		.replacement = {
			.target_type = "some/java/target_type_two",
			.target_name = "new_method_name",
			.is_static = true,
		}
	},
};

const JniRemappingIndexTypeEntry jni_remapping_method_replacement_index[] = {
	{
		.name = {
			.length = 18,
			.str = "some/java/type_one",
		},
		.method_count = 1,
		.methods = some_java_type_one_methods,
	},

	{
		.name = {
			.length = 18,
			.str = "some/java/type_two",
		},
		.method_count = 1,
		.methods = some_java_type_two_methods,
	},
};

const JniRemappingTypeReplacementEntry jni_remapping_type_replacements[] = {
	{
		.name = {
			.length = 14,
			.str = "some/java/type",
		},
		.replacement = "another/java/type",
	},

	{
		.name = {
			.length = 20,
			.str = "some/other/java/type",
		},
		.replacement = "another/replacement/java/type",
	},
};

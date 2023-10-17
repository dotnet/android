// Dear Emacs, this is a -*- C++ -*- header
#ifndef __XAMARIN_ANDROID_TYPEMAP_H
#define __XAMARIN_ANDROID_TYPEMAP_H

#include <stdint.h>

#include <jni.h>
#include <mono/metadata/image.h>

#include "monodroid.h"
#include "xxhash.hh"

static constexpr uint64_t FORMAT_TAG = 0x015E6972616D58;
static constexpr uint32_t MODULE_MAGIC_NAMES = 0x53544158; // 'XATS', little-endian
static constexpr uint32_t MODULE_INDEX_MAGIC = 0x49544158; // 'XATI', little-endian
static constexpr uint8_t  MODULE_FORMAT_VERSION = 2;       // Keep in sync with the value in src/Xamarin.Android.Build.Tasks/Utilities/TypeMapGenerator.cs

#if defined (DEBUG) || !defined (ANDROID)
struct BinaryTypeMapHeader
{
	uint32_t magic;
	uint32_t version;
	uint32_t entry_count;
	uint32_t java_name_width;
	uint32_t managed_name_width;
	uint32_t assembly_name_length;
};

struct TypeMapIndexHeader
{
	uint32_t magic;
	uint32_t version;
	uint32_t entry_count;
	uint32_t module_file_name_width;
};

struct TypeMapEntry
{
	const char *from;
	const char *to;
};

// MUST match src/Xamarin.Android.Build.Tasks/Utilities/TypeMappingDebugNativeAssemblyGenerator.cs
struct TypeMap
{
	uint32_t             entry_count;
	char                *assembly_name;
	uint8_t             *data;
	const TypeMapEntry  *java_to_managed;
	const TypeMapEntry  *managed_to_java;
};
#else
struct TypeMapModuleEntry
{
	uint32_t       type_token_id;
	uint32_t       java_map_index;
};

struct TypeMapModule
{
	uint8_t                   module_uuid[16];
	uint32_t                  entry_count;
	uint32_t                  duplicate_count;
	TypeMapModuleEntry const *map;
	TypeMapModuleEntry const *duplicate_map;
	char const               *assembly_name;
	MonoImage                *image;
	uint32_t                  java_name_width;
	uint8_t                  *java_map;
};

struct TypeMapJava
{
	uint32_t module_index;
	uint32_t type_token_id;
	uint32_t java_name_index;
};
#endif

struct XamarinAndroidBundledAssembly final
{
	int32_t  apk_fd;
	uint32_t data_offset;
	uint32_t data_size;
	uint8_t *data;
	uint32_t name_length;
	char    *name;
};

struct AssemblyEntry
{
	// offset into the `xa_input_assembly_data` array
	uint32_t input_data_offset;

	// number of bytes data of this assembly occupies
	uint32_t input_data_size;

	// offset into the `xa_uncompressed_assembly_data` array where the uncompressed
	// assembly data (if any) lives.
	uint32_t uncompressed_data_offset;

	// Size of the uncompressed data. 0 if assembly wasn't compressed.
	uint32_t uncompressed_data_size;
};

struct AssemblyIndexEntry
{
	xamarin::android::hash_t name_hash;

	// Index into the `xa_assemblies` descriptor array
	uint32_t assemblies_index;

	// Index into the `xa_load_info` array.  We can't reuse the `assemblies_index` above because the order
	// of entries in `xa_load_info` is determined in a different task than that of `xa_assemblies` and it
	// also depends on the number of assemblies placed in the standalone DSOs.
	uint32_t load_info_index;

	// whether hashed name had extension
	bool has_extension;

	// whether assembly data lives in a separate DSO
	bool is_standalone;
};

struct AssemblyLoadInfo
{
	uint32_t apk_offset; // offset into the APK, or 0 if the assembly isn't in a standalone DSO or if the DSOs are
						 // extracted to disk at install time
	void *mmap_addr;     // Address at which the assembly data was mmapped
};

constexpr uint32_t InputAssemblyDataSize = 1024;
constexpr uint32_t UncompressedAssemblyDataSize = 2048;
constexpr uint32_t AssemblyCount = 2;
constexpr uint32_t AssemblyNameLength = 26; // including the terminating NUL
constexpr uint32_t SharedLibraryNameLength = 32; // including the terminating NUL

struct AssembliesConfig
{
	uint32_t input_assembly_data_size;
	uint32_t uncompressed_assembly_data_size;
	uint32_t assembly_name_length;
	uint32_t assembly_count;
	uint32_t assembly_index_count;
	uint32_t assembly_dso_count;
	uint32_t shared_library_name_length;
};

MONO_API MONO_API_EXPORT const AssembliesConfig xa_assemblies_config;
MONO_API MONO_API_EXPORT const uint8_t xa_input_assembly_data[InputAssemblyDataSize];

// All the compressed assemblies are uncompressed into this array, with offsets in `xa_assemblies`
// pointing to the place where they start
MONO_API MONO_API_EXPORT uint8_t xa_uncompressed_assembly_data[UncompressedAssemblyDataSize];

MONO_API MONO_API_EXPORT const AssemblyEntry xa_assemblies[AssemblyCount];
MONO_API MONO_API_EXPORT const AssemblyIndexEntry xa_assembly_index[AssemblyCount];
MONO_API MONO_API_EXPORT const char xa_assembly_names[AssemblyCount][AssemblyNameLength];
MONO_API MONO_API_EXPORT const char xa_assembly_dso_names[AssemblyCount][SharedLibraryNameLength];
MONO_API MONO_API_EXPORT AssemblyLoadInfo xa_assemblies_load_info[AssemblyCount];

enum class MonoComponent : uint32_t
{
	None      = 0x00,
	Debugger  = 0x01,
	HotReload = 0x02,
	Tracing   = 0x04,
};

struct ApplicationConfig
{
	bool uses_mono_llvm;
	bool uses_mono_aot;
	bool aot_lazy_load;
	bool uses_assembly_preload;
	bool broken_exception_transitions;
	bool instant_run_enabled;
	bool jni_add_native_method_registration_attribute_present;
	bool have_runtime_config_blob;
	bool have_standalone_assembly_dsos;
	bool marshal_methods_enabled;
	uint8_t bound_exception_type;
	uint32_t package_naming_policy;
	uint32_t environment_variable_count;
	uint32_t system_property_count;
	uint32_t number_of_assemblies_in_apk;
	uint32_t bundled_assembly_name_width;
	uint32_t number_of_dso_cache_entries;
	uint32_t android_runtime_jnienv_class_token;
	uint32_t jnienv_initialize_method_token;
	uint32_t jnienv_registerjninatives_method_token;
	uint32_t jni_remapping_replacement_type_count;
	uint32_t jni_remapping_replacement_method_index_entry_count;
	MonoComponent mono_components_mask;
	const char *android_package_name;
};

struct DSOCacheEntry
{
	uint64_t       hash;
	bool           ignore;
	const char    *name;
	void          *handle;
};

struct JniRemappingString
{
	const uint32_t  length;
	const char     *str;
};

struct JniRemappingReplacementMethod
{
	const char    *target_type;
	const char    *target_name;
	// const char    *target_signature;
	// const int32_t  param_count;
	const bool     is_static;
};

struct JniRemappingIndexMethodEntry
{
	const JniRemappingString            name;
	const JniRemappingString            signature;
	const JniRemappingReplacementMethod replacement;
};

struct JniRemappingIndexTypeEntry
{
	const JniRemappingString            name;
	const uint32_t             method_count;
	const JniRemappingIndexMethodEntry *methods;
};

struct JniRemappingTypeReplacementEntry
{
	const JniRemappingString  name;
	const char      *replacement;
};

MONO_API MONO_API_EXPORT const JniRemappingIndexTypeEntry jni_remapping_method_replacement_index[];
MONO_API MONO_API_EXPORT const JniRemappingTypeReplacementEntry jni_remapping_type_replacements[];

MONO_API MONO_API_EXPORT const uint64_t format_tag;

#if defined (DEBUG) || !defined (ANDROID)
MONO_API MONO_API_EXPORT const TypeMap type_map; // MUST match src/Xamarin.Android.Build.Tasks/Utilities/TypeMappingDebugNativeAssemblyGenerator.cs
#else
MONO_API MONO_API_EXPORT const uint32_t map_module_count;
MONO_API MONO_API_EXPORT const uint32_t java_type_count;
MONO_API MONO_API_EXPORT const char* const java_type_names[];
MONO_API MONO_API_EXPORT TypeMapModule map_modules[];
MONO_API MONO_API_EXPORT const TypeMapJava map_java[];
MONO_API MONO_API_EXPORT const xamarin::android::hash_t map_java_hashes[];
#endif

MONO_API MONO_API_EXPORT const ApplicationConfig application_config;
MONO_API MONO_API_EXPORT const char* const app_environment_variables[];
MONO_API MONO_API_EXPORT const char* const app_system_properties[];

MONO_API MONO_API_EXPORT const char* const mono_aot_mode_name;

MONO_API MONO_API_EXPORT XamarinAndroidBundledAssembly bundled_assemblies[];

MONO_API MONO_API_EXPORT DSOCacheEntry dso_cache[];

//
// Support for marshal methods
//
#if defined (RELEASE) && defined (ANDROID) && defined (NET)
struct MarshalMethodsManagedClass
{
	const uint32_t   token;
	MonoClass       *klass;
};

// Number of assembly name forms for which we generate hashes (essentially file name mutations. For instance
// `HelloWorld.dll`, `HelloWorld`, `en-US/HelloWorld` etc). This is multiplied by the number of assemblies in the apk to
// obtain number of entries in the `assembly_image_cache_hashes` and `assembly_image_cache_indices` entries
constexpr uint32_t number_of_assembly_name_forms_in_image_cache = 2;

// These 3 arrays constitute the cache used to store pointers to loaded managed assemblies.
// Three arrays are used so that we can have multiple hashes pointing to the same MonoImage*.
//
// This is done by the `assembly_image_cache_hashes` containing hashes for all mutations of some
// assembly's name (e.g. with culture prefix, without extension etc) and position of that hash in
// `assembly_image_cache_hashes` is an index into `assembly_image_cache_indices` which, in turn,
// stores final index into the `assembly_image_cache` array.
//
MONO_API MONO_API_EXPORT MonoImage* assembly_image_cache[];
MONO_API MONO_API_EXPORT const uint32_t assembly_image_cache_indices[];
MONO_API MONO_API_EXPORT const xamarin::android::hash_t assembly_image_cache_hashes[];

// Number of unique classes which contain native callbacks we bind
MONO_API MONO_API_EXPORT uint32_t marshal_methods_number_of_classes;
MONO_API MONO_API_EXPORT MarshalMethodsManagedClass marshal_methods_class_cache[];

//
// These tables store names of classes and managed callback methods used in the generated marshal methods
// code. They are used just for error reporting.
//
// Class names are found at the same indexes as their corresponding entries in the `marshal_methods_class_cache` array
// above. Method names are stored as token:name pairs and the array must end with an "invalid" terminator entry (token
// == 0; name == nullptr)
//
struct MarshalMethodName
{
	// combination of assembly index (high 32 bits) and method token (low 32 bits)
	const uint64_t  id;
	const char     *name;
};

MONO_API MONO_API_EXPORT const char* const mm_class_names[];
MONO_API MONO_API_EXPORT const MarshalMethodName mm_method_names[];

using get_function_pointer_fn = void(*)(uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void*& target_ptr);

MONO_API MONO_API_EXPORT void xamarin_app_init (JNIEnv *env, get_function_pointer_fn fn) noexcept;
#endif // def RELEASE && def ANDROID && def NET

#endif // __XAMARIN_ANDROID_TYPEMAP_H

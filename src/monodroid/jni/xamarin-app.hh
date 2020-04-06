// Dear Emacs, this is a -*- C++ -*- header
#ifndef __XAMARIN_ANDROID_TYPEMAP_H
#define __XAMARIN_ANDROID_TYPEMAP_H

#include <stdint.h>

#include <mono/metadata/image.h>

#include "monodroid.h"

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
	TypeMapEntry        *java_to_managed;
	TypeMapEntry        *managed_to_java;
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
	TypeMapModuleEntry       *map;
	TypeMapModuleEntry       *duplicate_map;
	char                     *assembly_name;
	MonoImage                *image;
	uint32_t                  java_name_width;
	uint8_t                  *java_map;
};

struct TypeMapJava
{
	uint32_t module_index;
	uint32_t type_token_id;
	uint8_t  java_name[];
};
#endif

struct ApplicationConfig
{
	bool uses_mono_llvm;
	bool uses_mono_aot;
	bool uses_assembly_preload;
	bool is_a_bundled_app;
	bool broken_exception_transitions;
	bool instant_run_enabled;
	bool jni_add_native_method_registration_attribute_present;
	uint8_t bound_exception_type;
	uint32_t package_naming_policy;
	uint32_t environment_variable_count;
	uint32_t system_property_count;
	const char *android_package_name;
};

#if defined (DEBUG) || !defined (ANDROID)
MONO_API const TypeMap type_map; // MUST match src/Xamarin.Android.Build.Tasks/Utilities/TypeMappingDebugNativeAssemblyGenerator.cs
#else
MONO_API const uint32_t map_module_count;
MONO_API const uint32_t java_type_count;
MONO_API const uint32_t java_name_width;
MONO_API const TypeMapModule map_modules[];
MONO_API const TypeMapJava map_java[];
#endif

MONO_API ApplicationConfig application_config;
MONO_API const char* app_environment_variables[];
MONO_API const char* app_system_properties[];

MONO_API const char* mono_aot_mode_name;
#endif // __XAMARIN_ANDROID_TYPEMAP_H

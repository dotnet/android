// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <array>
#include <cstdint>

#include <jni.h>
#include <runtime-base/crc32.hh>

static constexpr uint64_t FORMAT_TAG = 0x00045E6972616D58; // 'Xmari^XY' where XY is the format version
static constexpr uint32_t COMPRESSED_DATA_MAGIC = 0x535A4158; // 'XAZS', little-endian
static constexpr uint32_t ASSEMBLY_STORE_MAGIC = 0x41424158; // 'XABA', little-endian

// The highest bit of assembly store version is a 64-bit ABI flag
#if INTPTR_MAX == INT64_MAX
static constexpr uint32_t ASSEMBLY_STORE_64BIT_FLAG = 0x80000000;
#else
static constexpr uint32_t ASSEMBLY_STORE_64BIT_FLAG = 0x00000000;
#endif

// The second-to-last byte denotes the actual ABI
#if defined(__aarch64__)
static constexpr uint32_t ASSEMBLY_STORE_ABI = 0x00010000;
#elif defined(__arm__)
static constexpr uint32_t ASSEMBLY_STORE_ABI = 0x00020000;
#elif defined(__x86_64__)
static constexpr uint32_t ASSEMBLY_STORE_ABI = 0x00030000;
#elif defined(__i386__)
static constexpr uint32_t ASSEMBLY_STORE_ABI = 0x00040000;
#endif

// Increase whenever an incompatible change is made to the assembly store format
static constexpr uint32_t ASSEMBLY_STORE_FORMAT_VERSION = 3 | ASSEMBLY_STORE_64BIT_FLAG | ASSEMBLY_STORE_ABI;

struct CompressedAssemblyHeader
{
	uint32_t magic; // COMPRESSED_DATA_MAGIC
	uint32_t descriptor_index;
	uint32_t uncompressed_length;
};

struct CompressedAssemblyDescriptor
{
	uint32_t   uncompressed_file_size;
	bool       loaded;
	uint32_t   buffer_offset;
};


//
// Assembly store format
//
// Each target ABI/architecture has a single assembly store file, composed of the following parts:
//
// [HEADER]
// [INDEX]
// [ASSEMBLY_DESCRIPTORS]
// [ASSEMBLY_NAMES]
// [ASSEMBLY DATA]
//
// Formats of the sections above are as follows:
//
// HEADER (fixed size)
//  [MAGIC]              uint; value: 0x41424158
//  [FORMAT_VERSION]     uint; store format version number
//  [ENTRY_COUNT]        uint; number of entries in the store
//  [INDEX_ENTRY_COUNT]  uint; number of entries in the index
//  [INDEX_SIZE]         uint; index size in bytes
//
// INDEX (variable size, HEADER.ENTRY_COUNT*2 entries, for assembly names with and without the extension)
//  [NAME_HASH]          uint; CRC32 of the assembly name
//  [DESCRIPTOR_INDEX]   uint; index into in-store assembly descriptor array
//  [IGNORE]             byte; if set to anything other than 0, the assembly is to be ignored when loading
//
// ASSEMBLY_DESCRIPTORS (variable size, HEADER.ENTRY_COUNT entries), each entry formatted as follows:
//  [MAPPING_INDEX]      uint; index into a runtime array where assembly data pointers are stored
//  [DATA_OFFSET]        uint; offset from the beginning of the store to the start of assembly data
//  [DATA_SIZE]          uint; size of the stored assembly data
//  [DEBUG_DATA_OFFSET]  uint; offset from the beginning of the store to the start of assembly PDB data, 0 if absent
//  [DEBUG_DATA_SIZE]    uint; size of the stored assembly PDB data, 0 if absent
//  [CONFIG_DATA_OFFSET] uint; offset from the beginning of the store to the start of assembly .config contents, 0 if absent
//  [CONFIG_DATA_SIZE]   uint; size of the stored assembly .config contents, 0 if absent
//
// ASSEMBLY_NAMES (variable size, HEADER.ENTRY_COUNT entries), each entry formatted as follows:
//  [NAME_LENGTH]        uint: length of assembly name
//  [NAME]               byte: UTF-8 bytes of assembly name, without the NUL terminator
//

//
// The structures which are found in the store files must be packed to avoid problems when calculating offsets (runtime
// size of a structure can be different than the real data size)
//
struct [[gnu::packed]] AssemblyStoreHeader final
{
	uint32_t magic;
	uint32_t version;
	uint32_t entry_count;
	uint32_t index_entry_count;
	uint32_t index_size; // index size in bytes
};

struct [[gnu::packed]] AssemblyStoreIndexEntry final
{
	xamarin::android::hash_t name_hash;
	uint32_t descriptor_index;
	uint8_t ignore; // Assembly should be ignored when loading, its data isn't actually there
};

struct [[gnu::packed]] AssemblyStoreEntryDescriptor final
{
	uint32_t mapping_index;

	uint32_t data_offset;
	uint32_t data_size;

	uint32_t debug_data_offset;
	uint32_t debug_data_size;

	uint32_t config_data_offset;
	uint32_t config_data_size;
};

struct AssemblyStoreRuntimeData final
{
	const uint8_t       *data_start;
	uint32_t             assembly_count;
	uint32_t             index_entry_count;
	const AssemblyStoreEntryDescriptor *assemblies;
};

struct AssemblyStoreSingleAssemblyRuntimeData final
{
	const uint8_t       *image_data;
	const uint8_t       *debug_info_data;
	const uint8_t       *config_data;
	const AssemblyStoreEntryDescriptor *descriptor;
};

// Keep in strict sync with:
//   src/Xamarin.Android.Build.Tasks/Utilities/ApplicationConfigCLR.cs
//   src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Utilities/EnvironmentHelper.cs
struct ApplicationConfig
{
	bool uses_assembly_preload;
	bool marshal_methods_enabled;
	bool ignore_split_configs;
	uint32_t number_of_runtime_properties;
	uint32_t package_naming_policy;
	uint32_t environment_variable_count;
	uint32_t system_property_count;
	uint32_t number_of_assemblies_in_apk;
	uint32_t bundled_assembly_name_width;
	uint32_t number_of_dso_cache_entries;
	uint32_t number_of_shared_libraries;
	uint32_t android_runtime_jnienv_class_token;
	uint32_t jnienv_initialize_method_token;
	uint32_t jni_remapping_replacement_type_count;
	uint32_t jni_remapping_replacement_method_index_entry_count;
	const char *android_package_name;
	bool have_assembly_store;
};

struct DSOCacheEntry
{
	const xamarin::android::hash_t hash;
	const bool      ignore;
	const bool      is_jni_library;
	const uint32_t  name_index;
	void           *handle;
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

struct AppEnvironmentVariable
{
	const uint32_t name_index;
	const uint32_t value_index;
};

extern "C" {
	[[gnu::visibility("default")]] extern const JniRemappingIndexTypeEntry jni_remapping_method_replacement_index[];
	[[gnu::visibility("default")]] extern const JniRemappingTypeReplacementEntry jni_remapping_type_replacements[];

	[[gnu::visibility("default")]] extern const uint64_t format_tag;

	[[gnu::visibility("default")]] extern uint32_t compressed_assembly_count;
	[[gnu::visibility("default")]] extern CompressedAssemblyDescriptor compressed_assembly_descriptors[];
	[[gnu::visibility("default")]] extern uint32_t uncompressed_assemblies_data_size;
	[[gnu::visibility("default")]] extern uint8_t uncompressed_assemblies_data_buffer[];
	[[gnu::visibility("default")]] extern const ApplicationConfig application_config;
	[[gnu::visibility("default")]] extern const AppEnvironmentVariable app_environment_variables[];
	[[gnu::visibility("default")]] extern const char app_environment_variable_contents[];
	[[gnu::visibility("default")]] extern const AppEnvironmentVariable app_system_properties[];
	[[gnu::visibility("default")]] extern const char app_system_property_contents[];

	[[gnu::visibility("default")]] extern AssemblyStoreSingleAssemblyRuntimeData assembly_store_bundled_assemblies[];
	[[gnu::visibility("default")]] extern AssemblyStoreRuntimeData assembly_store;

	[[gnu::visibility("default")]] extern DSOCacheEntry dso_cache[];
	[[gnu::visibility("default")]] extern const uint dso_jni_preloads_idx_stride;
	[[gnu::visibility("default")]] extern const uint dso_jni_preloads_idx_count;
	[[gnu::visibility("default")]] extern const uint dso_jni_preloads_idx[];
	[[gnu::visibility("default")]] extern const char dso_names_data[];

	[[gnu::visibility("default")]] extern const char *init_runtime_property_names[];
	[[gnu::visibility("default")]] extern char *init_runtime_property_values[];
}

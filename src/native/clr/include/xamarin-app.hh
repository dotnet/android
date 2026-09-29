// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <array>
#include <cstdint>

#include <jni.h>
#include <runtime-base/crc32.hh>

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

struct ApplicationConfig
{
	uint32_t number_of_runtime_properties;
	uint32_t package_naming_policy;
	const char *android_package_name;
	bool have_assembly_store;
};

extern AssemblyStoreRuntimeData assembly_store;

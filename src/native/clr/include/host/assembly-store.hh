#pragma once

#include <cstdint>
#include <limits>
#include <string>
#include <string_view>
#include <tuple>

#include <runtime-base/mutex.hh>
#include <xamarin-app.hh>

namespace xamarin::android {
	class AssemblyStore
	{
	public:
		static auto open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*;

		// Configure the store directly from an in-memory payload pointer (obtained via
		// dlopen()+dlsym() of the `_assembly_store` dynamic symbol). The payload is mapped
		// read-only and is never modified, so it (and every pointer derived from it) is `const`.
		// The XABA store is trusted build-generated data; no external payload length is available.
		// `store_path` is used only in diagnostic messages and may be `nullptr` - every use of it
		// goes through `optional_string ()`.
		// Configure before assembly probes run. Sequential reconfiguration retains previously
		// returned image buffers for the process lifetime required by CoreCLR.
		static void configure_from_payload (const void *payload_start, const char *store_path) noexcept;

	private:
		static void set_assembly_data_and_size (uint8_t* source_assembly_data, uint32_t source_assembly_data_size, uint8_t*& dest_assembly_data, uint32_t& dest_assembly_data_size) noexcept;

		// Returns a tuple of <assembly_data_pointer, data_size>
		static auto get_assembly_data (AssemblyStoreSingleAssemblyRuntimeData const& e, std::string_view const& name) noexcept -> std::tuple<uint8_t*, uint32_t>;
		static auto find_assembly_store_entry (std::string_view const& name, hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept -> const AssemblyStoreIndexEntry*;

	private:
		static inline AssemblyStoreRuntimeData assembly_store {};
		static inline const AssemblyStoreIndexEntry *assembly_store_hashes = nullptr;
		static inline AssemblyStoreSingleAssemblyRuntimeData *runtime_assemblies = nullptr;
		static inline CompressedAssemblyDescriptor *compressed_descriptors = nullptr;
		static inline uint8_t *uncompressed_buffer = nullptr;
		static inline uint32_t compressed_count = 0;
		static inline uint32_t uncompressed_size = 0;
		// Assembly names indexed by `AssemblyStoreIndexEntry::descriptor_index`, used to disambiguate
		// CRC32 hash collisions in the store index. Built once when the store is mapped.
		static inline std::string_view *assembly_store_names = nullptr;
		static inline pthread_mutex_t assembly_decompress_mutex = PTHREAD_MUTEX_INITIALIZER;
	};
}

#pragma once

#include <cstdint>
#include <limits>
#include <mutex>
#include <string_view>
#include <tuple>
#include <vector>

#include <xamarin-app.hh>
#include <runtime-base/strings.hh>

namespace xamarin::android {
	class AssemblyStore
	{
	public:
		static auto open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*;

		static void map (int fd, std::string_view const& apk_path, std::string_view const& store_path, uint32_t offset, uint32_t size, bool writable = false) noexcept;

		static void map (int fd, std::string_view const& file_path, uint32_t offset, uint32_t size, bool writable = false) noexcept
		{
			map (fd, {}, file_path, offset, size, writable);
		}

		// EXPERIMENT (assemblystore-mmap): find the DEFLATE-compressed assembly store asset in
		// the given APK, extract it once (uncompressed) into `override_dir`, and mmap it from
		// disk. Returns `true` if the store was found and mapped.
		static bool try_extract_and_map (std::string_view const& apk_path, std::string_view const& override_dir) noexcept;

		// EXPERIMENT (assemblystore-mmap hybrid): register an assembly (e.g. a per-ABI ReadyToRun
		// composite image) that lives outside the shared assembly store, mmap'd from a DSO-wrapped
		// `lib/<abi>/*.so` entry in the APK, so `open_assembly` can serve it by name.
		static void register_extra_assembly (std::string_view const& name, int fd, std::string_view const& apk_path, uint32_t offset, uint32_t size) noexcept;

	private:
		static void set_assembly_data_and_size (uint8_t* source_assembly_data, uint32_t source_assembly_data_size, uint8_t*& dest_assembly_data, uint32_t& dest_assembly_data_size) noexcept;

		// Returns a tuple of <assembly_data_pointer, data_size>
		static auto get_assembly_data (AssemblyStoreSingleAssemblyRuntimeData const& e, std::string_view const& name) noexcept -> std::tuple<uint8_t*, uint32_t>;
		static auto find_assembly_store_entry (hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept -> const AssemblyStoreIndexEntry*;

	private:
		static inline AssemblyStoreIndexEntry *assembly_store_hashes = nullptr;
		static inline std::mutex  assembly_decompress_mutex {};

		// EXPERIMENT (assemblystore-mmap hybrid): out-of-store assemblies (ReadyToRun composites)
		// keyed by name hash, each an in-memory pointer + size.
		struct ExtraAssembly {
			hash_t   name_hash;
			uint8_t *data;
			uint32_t size;
		};
		static inline std::vector<ExtraAssembly> extra_assemblies {};
	};
}

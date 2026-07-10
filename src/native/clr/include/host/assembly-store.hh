#pragma once

#include <cstdint>
#include <limits>
#include <mutex>
#include <string_view>
#include <tuple>

#include <xamarin-app.hh>
#include <runtime-base/strings.hh>

namespace xamarin::android {
	class AssemblyStore
	{
	public:
		static auto open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*;

		static void map (int fd, std::string_view const& apk_path, std::string_view const& store_path, uint32_t offset, uint32_t size) noexcept;

		// Configure the store directly from an in-memory payload pointer (e.g. obtained via
		// dlopen()+dlsym() of the `_assembly_store_start` dynamic symbol) instead of locating and
		// mmapping it out of the APK.
		static void map_from_pointer (void *payload_start, std::string_view const& description) noexcept;

		static void map (int fd, std::string_view const& file_path, uint32_t offset, uint32_t size) noexcept
		{
			map (fd, {}, file_path, offset, size);
		}

	private:
		template<typename TFullPathProvider>
		static void configure_from_payload (void *payload_start, TFullPathProvider get_full_store_path) noexcept;

		static void set_assembly_data_and_size (uint8_t* source_assembly_data, uint32_t source_assembly_data_size, uint8_t*& dest_assembly_data, uint32_t& dest_assembly_data_size) noexcept;

		// Returns a tuple of <assembly_data_pointer, data_size>
		static auto get_assembly_data (AssemblyStoreSingleAssemblyRuntimeData const& e, std::string_view const& name) noexcept -> std::tuple<uint8_t*, uint32_t>;
		static auto find_assembly_store_entry (hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept -> const AssemblyStoreIndexEntry*;

	private:
		static inline AssemblyStoreIndexEntry *assembly_store_hashes = nullptr;
		static inline std::mutex  assembly_decompress_mutex {};
	};
}

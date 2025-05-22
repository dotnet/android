#pragma once

#include <cstdint>
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

		static void map (int fd, std::string_view const& file_path, uint32_t offset, uint32_t size) noexcept
		{
			map (fd, {}, file_path, offset, size);
		}

	private:
		static void set_assembly_data_and_size (uint8_t* source_assembly_data, uint32_t source_assembly_data_size, uint8_t*& dest_assembly_data, uint32_t& dest_assembly_data_size) noexcept;

		// Returns a tuple of <assembly_data_pointer, data_size>
		static auto get_assembly_data (AssemblyStoreSingleAssemblyRuntimeData const& e, std::string_view const& name) noexcept -> std::tuple<uint8_t*, uint32_t>;
		static auto find_assembly_store_entry (hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept -> const AssemblyStoreIndexEntry*;

	private:
		static inline AssemblyStoreIndexEntry *assembly_store_hashes = nullptr;
		static inline std::mutex  assembly_decompress_mutex {};
	};
}

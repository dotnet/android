#pragma once

#include <cstdint>
#include <functional>
#include <limits>
#include <mutex>
#include <string>
#include <string_view>
#include <tuple>

#include <xamarin-app.hh>
#include <runtime-base/strings.hh>

namespace xamarin::android {
	class AssemblyStore
	{
	public:
		static auto open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*;

		// Configure the store directly from an in-memory payload pointer (obtained via
		// dlopen()+dlsym() of the `_assembly_store` dynamic symbol). The payload is mapped
		// read-only and is never modified, so it (and every pointer derived from it) is `const`.
		// `get_full_store_path` is invoked only to build diagnostics if the payload turns out
		// to be invalid.
		static void configure_from_payload (const void *payload_start, const std::function<std::string()>& get_full_store_path) noexcept;

	private:
		static void set_assembly_data_and_size (uint8_t* source_assembly_data, uint32_t source_assembly_data_size, uint8_t*& dest_assembly_data, uint32_t& dest_assembly_data_size) noexcept;

		// Returns a tuple of <assembly_data_pointer, data_size>
		static auto get_assembly_data (AssemblyStoreSingleAssemblyRuntimeData const& e, std::string_view const& name) noexcept -> std::tuple<uint8_t*, uint32_t>;
		static auto find_assembly_store_entry (hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept -> const AssemblyStoreIndexEntry*;

	private:
		static inline const AssemblyStoreIndexEntry *assembly_store_hashes = nullptr;
		static inline std::mutex  assembly_decompress_mutex {};
	};
}

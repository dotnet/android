#include <string>

#if defined (HAVE_LZ4)
#include <lz4.h>
#endif

#include <xamarin-app.hh>
#include <host/assembly-store.hh>
#include <runtime-base/util.hh>
#include <runtime-base/search.hh>
#include <runtime-base/startup-aware-lock.hh>

using namespace xamarin::android;

[[gnu::always_inline]]
void AssemblyStore::set_assembly_data_and_size (uint8_t* source_assembly_data, uint32_t source_assembly_data_size, uint8_t*& dest_assembly_data, uint32_t& dest_assembly_data_size) noexcept
{
	dest_assembly_data = source_assembly_data;
	dest_assembly_data_size = source_assembly_data_size;
}

[[gnu::always_inline]]
auto AssemblyStore::get_assembly_data (AssemblyStoreSingleAssemblyRuntimeData const& e, std::string_view const& name) noexcept -> std::tuple<uint8_t*, uint32_t>
{
	uint8_t *assembly_data = nullptr;
	uint32_t assembly_data_size = 0;

#if defined (HAVE_LZ4) && defined (RELEASE)
	auto header = reinterpret_cast<const CompressedAssemblyHeader*>(e.image_data);
	if (header->magic == COMPRESSED_DATA_MAGIC) {
		if (compressed_assemblies.descriptors == nullptr) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Compressed assembly found but no descriptor defined"sv);
		}
		if (header->descriptor_index >= compressed_assemblies.count) [[unlikely]] {
			Helpers::abort_application (
				LOG_ASSEMBLY,
				std::format (
					"Invalid compressed assembly descriptor index {}",
					header->descriptor_index
				)
			);
		}

		CompressedAssemblyDescriptor &cad = compressed_assemblies.descriptors[header->descriptor_index];
		assembly_data_size = e.descriptor->data_size - sizeof(CompressedAssemblyHeader);
		if (!cad.loaded) {
			StartupAwareLock decompress_lock (assembly_decompress_mutex);

			if (cad.loaded) {
				set_assembly_data_and_size (reinterpret_cast<uint8_t*>(cad.data), cad.uncompressed_file_size, assembly_data, assembly_data_size);
				return {assembly_data, assembly_data_size};
			}

			if (cad.data == nullptr) [[unlikely]] {
				Helpers::abort_application (
					LOG_ASSEMBLY,
					std::format (
						"Invalid compressed assembly descriptor at {}: no data",
						header->descriptor_index
					)
				);
			}

			if (header->uncompressed_length != cad.uncompressed_file_size) {
				if (header->uncompressed_length > cad.uncompressed_file_size) {
					Helpers::abort_application (
						LOG_ASSEMBLY,
						std::format (
							"Compressed assembly '{}' is larger than when the application was built (expected at most {}, got {}). Assemblies don't grow just like that!",
							name,
							cad.uncompressed_file_size,
							header->uncompressed_length
						)
					);
				} else {
					log_debug (LOG_ASSEMBLY, "Compressed assembly '{}' is smaller than when the application was built. Adjusting accordingly.", name);
				}
				cad.uncompressed_file_size = header->uncompressed_length;
			}

			const char *data_start = pointer_add<const char*>(e.image_data, sizeof(CompressedAssemblyHeader));
			int ret = LZ4_decompress_safe (data_start, reinterpret_cast<char*>(cad.data), static_cast<int>(assembly_data_size), static_cast<int>(cad.uncompressed_file_size));

			if (ret < 0) {
				Helpers::abort_application (
					LOG_ASSEMBLY,
					std::format (
						"Decompression of assembly {} failed with code {}",
						name,
						ret
					)
				);
			}

			if (static_cast<uint64_t>(ret) != cad.uncompressed_file_size) {
				Helpers::abort_application (
					LOG_ASSEMBLY,
					std::format (
						"Decompression of assembly {} yielded a different size (expected {}, got {})",
						name,
						cad.uncompressed_file_size,
						static_cast<uint32_t>(ret)
					)
				);
			}
			cad.loaded = true;
		}

		set_assembly_data_and_size (reinterpret_cast<uint8_t*>(cad.data), cad.uncompressed_file_size, assembly_data, assembly_data_size);
	} else
#endif // def HAVE_LZ4 && def RELEASE
	{
		set_assembly_data_and_size (e.image_data, e.descriptor->data_size, assembly_data, assembly_data_size);
	}

	return {assembly_data, assembly_data_size};
}

[[gnu::always_inline]]
auto AssemblyStore::find_assembly_store_entry (hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept -> const AssemblyStoreIndexEntry*
{
	auto equal = [](AssemblyStoreIndexEntry const& entry, hash_t key) -> bool { return entry.name_hash == key; };
	auto less_than = [](AssemblyStoreIndexEntry const& entry, hash_t key) -> bool { return entry.name_hash < key; };
	ssize_t idx = Search::binary_search<AssemblyStoreIndexEntry, equal, less_than> (hash, entries, entry_count);
	if (idx >= 0) {
		return &entries[idx];
	}
	return nullptr;
}

auto AssemblyStore::open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*
{
	hash_t name_hash = xxhash::hash (name.data (), name.length ());
	log_debug (LOG_ASSEMBLY, "assembly_store_open_from_bundles: looking for bundled name: '{}' (hash {:x})", optional_string (name.data ()), name_hash);

	const AssemblyStoreIndexEntry *hash_entry = find_assembly_store_entry (name_hash, assembly_store_hashes, assembly_store.index_entry_count);
	if (hash_entry == nullptr) {
		log_warn (LOG_ASSEMBLY, "Assembly '{}' (hash 0x{:x}) not found", optional_string (name.data ()), name_hash);
		return nullptr;
	}

	if (hash_entry->descriptor_index >= assembly_store.assembly_count) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Invalid assembly descriptor index {}, exceeds the maximum value of {}",
				hash_entry->descriptor_index,
				assembly_store.assembly_count - 1
			)
		);
	}

	AssemblyStoreEntryDescriptor &store_entry = assembly_store.assemblies[hash_entry->descriptor_index];
	AssemblyStoreSingleAssemblyRuntimeData &assembly_runtime_info = assembly_store_bundled_assemblies[store_entry.mapping_index];

	if (assembly_runtime_info.image_data == nullptr) {
		// The assignments here don't need to be atomic, the value will always be the same, so even if two threads
		// arrive here at the same time, nothing bad will happen.
		assembly_runtime_info.image_data = assembly_store.data_start + store_entry.data_offset;
		assembly_runtime_info.descriptor = &store_entry;
		if (store_entry.debug_data_offset != 0) {
			assembly_runtime_info.debug_info_data = assembly_store.data_start + store_entry.debug_data_offset;
		}

		log_debug (
			LOG_ASSEMBLY,
			"Mapped: image_data == {:p}; debug_info_data == {:p}; config_data == {:p}; descriptor == {:p}; data size == {}; debug data size == {}; config data size == {}; name == '{}'",
			static_cast<void*>(assembly_runtime_info.image_data),
			static_cast<void*>(assembly_runtime_info.debug_info_data),
			static_cast<void*>(assembly_runtime_info.config_data),
			static_cast<void*>(assembly_runtime_info.descriptor),
			assembly_runtime_info.descriptor->data_size,
			assembly_runtime_info.descriptor->debug_data_size,
			assembly_runtime_info.descriptor->config_data_size,
			name
		);
	}

	auto [assembly_data, assembly_data_size] = get_assembly_data (assembly_runtime_info, name);
	size = assembly_data_size;
	return assembly_data;
}

void AssemblyStore::map (int fd, std::string_view const& apk_path, std::string_view const& store_path, uint32_t offset, uint32_t size) noexcept
{
	detail::mmap_info assembly_store_map = Util::mmap_file (fd, offset, size, store_path);

	auto [payload_start, payload_size] = Util::get_wrapper_dso_payload_pointer_and_size (assembly_store_map, store_path);
	log_debug (LOG_ASSEMBLY, "Adjusted assembly store pointer: {:p}; size: {}", payload_start, payload_size);
	auto header = static_cast<AssemblyStoreHeader*>(payload_start);

	auto get_full_store_path = [&apk_path, &store_path]() -> std::string {
		std::string full_store_path;

		if (!apk_path.empty ()) {
			full_store_path.append (apk_path);
			// store path will be relative, to the apk
			full_store_path.append ("!/"sv);
			full_store_path.append (store_path);
		} else {
			full_store_path.append (store_path);
		}

		return full_store_path;
	};

	if (header->magic != ASSEMBLY_STORE_MAGIC) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Assembly store '{}' is not a valid .NET for Android assembly store file",
				get_full_store_path ()
			)
		);
	}

	if (header->version != ASSEMBLY_STORE_FORMAT_VERSION) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Assembly store '{}' uses format version {:x}, instead of the expected {:x}",
				get_full_store_path (),
				header->version,
				ASSEMBLY_STORE_FORMAT_VERSION
			)
		);
	}

	constexpr size_t header_size = sizeof(AssemblyStoreHeader);

	assembly_store.data_start = static_cast<uint8_t*>(payload_start);
	assembly_store.assembly_count = header->entry_count;
	assembly_store.index_entry_count = header->index_entry_count;
	assembly_store.assemblies = reinterpret_cast<AssemblyStoreEntryDescriptor*>(assembly_store.data_start + header_size + header->index_size);
	assembly_store_hashes = reinterpret_cast<AssemblyStoreIndexEntry*>(assembly_store.data_start + header_size);

	log_debug (LOG_ASSEMBLY, "Mapped assembly store {}", get_full_store_path ());
}

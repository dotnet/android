#include <string>

#if defined (HAVE_LZ4)
#include <lz4.h>
#endif

#include <xamarin-app.hh>
#include <host/assembly-store.hh>
#include <runtime-base/util.hh>
#include <runtime-base/search.hh>
#include <runtime-base/startup-aware-lock.hh>
#include <runtime-base/timing-internal.hh>

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
		log_debug (LOG_ASSEMBLY, "Decompressing assembly '{}' from the assembly store"sv, name);

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.start_event (TimingEventKind::AssemblyDecompression);
		}

		if (compressed_assembly_count == 0) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Compressed assembly found but no descriptor defined"sv);
		}
		if (header->descriptor_index >= compressed_assembly_count) [[unlikely]] {
			Helpers::abort_application (
				LOG_ASSEMBLY,
				std::format (
					"Invalid compressed assembly descriptor index {}"sv,
					header->descriptor_index
				)
			);
		}

		CompressedAssemblyDescriptor &cad = compressed_assembly_descriptors[header->descriptor_index];
		assembly_data_size = e.descriptor->data_size - sizeof(CompressedAssemblyHeader);

		if (cad.buffer_offset >= uncompressed_assemblies_data_size) [[unlikely]] {
			Helpers::abort_application (
				LOG_ASSEMBLY,
				std::format (
					"Invalid compressed assembly buffer offset {}. Must be smaller than {}",
					cad.buffer_offset,
					uncompressed_assemblies_data_size
				)
			);
		}

		// This is not a perfect check, since we might be still within the buffer size and yet
		// have the tail end of this assembly's data overwritten by the next assembly's data, but
		// that will cause the app to crash when one or the the other assembly is loaded, so it's
		// OK to accept that risk. The whole situation is very, very unlikely.
		if (cad.uncompressed_file_size > uncompressed_assemblies_data_size - cad.buffer_offset) [[unlikely]] {
			Helpers::abort_application (
				LOG_ASSEMBLY,
				std::format (
					"Invalid compressed assembly buffer size {} at offset {}. Must not exceed {}",
					cad.uncompressed_file_size,
					cad.buffer_offset,
					uncompressed_assemblies_data_size - cad.buffer_offset
				)
			);
		}

		uint8_t *data_buffer = uncompressed_assemblies_data_buffer + cad.buffer_offset;
		if (!cad.loaded) {
			StartupAwareLock decompress_lock (assembly_decompress_mutex);

			if (cad.loaded) {
				set_assembly_data_and_size (data_buffer, cad.uncompressed_file_size, assembly_data, assembly_data_size);

				if (FastTiming::enabled ()) [[unlikely]] {
					internal_timing.end_event (true /* uses_more_info */);

					dynamic_local_string<SENSIBLE_TYPE_NAME_LENGTH> msg;
					msg.append (name);
					msg.append (" (decompressed in another thread)"sv);
					internal_timing.add_more_info (msg);
				}
				return {assembly_data, assembly_data_size};
			}

			if (header->uncompressed_length != cad.uncompressed_file_size) {
				if (header->uncompressed_length > cad.uncompressed_file_size) {
					Helpers::abort_application (
						LOG_ASSEMBLY,
						std::format (
							"Compressed assembly '{}' is larger than when the application was built (expected at most {}, got {}). Assemblies don't grow just like that!"sv,
							name,
							cad.uncompressed_file_size,
							header->uncompressed_length
						)
					);
				} else {
					log_debug (LOG_ASSEMBLY, "Compressed assembly '{}' is smaller than when the application was built. Adjusting accordingly."sv, name);
				}
				cad.uncompressed_file_size = header->uncompressed_length;
			}

			const char *data_start = pointer_add<const char*>(e.image_data, sizeof(CompressedAssemblyHeader));
			int ret = LZ4_decompress_safe (data_start, reinterpret_cast<char*>(data_buffer), static_cast<int>(assembly_data_size), static_cast<int>(cad.uncompressed_file_size));

			if (ret < 0) {
				Helpers::abort_application (
					LOG_ASSEMBLY,
					std::format (
						"Decompression of assembly {} failed with code {}"sv,
						name,
						ret
					)
				);
			}

			if (static_cast<uint64_t>(ret) != cad.uncompressed_file_size) {
				Helpers::abort_application (
					LOG_ASSEMBLY,
					std::format (
						"Decompression of assembly {} yielded a different size (expected {}, got {})"sv,
						name,
						cad.uncompressed_file_size,
						static_cast<uint32_t>(ret)
					)
				);
			}
			cad.loaded = true;
			if (FastTiming::enabled ()) [[unlikely]] {
				internal_timing.end_event (true /* uses_more_info */);
				internal_timing.add_more_info (name);
			}
		}

		set_assembly_data_and_size (data_buffer, cad.uncompressed_file_size, assembly_data, assembly_data_size);
	} else
#endif // def HAVE_LZ4 && def RELEASE
	{
		log_debug (LOG_ASSEMBLY, "Assembly '{}' is not compressed in the assembly store"sv, name);

		// HACK! START
		// Currently, MAUI crashes when we return a pointer to read-only data, so we must copy
		// the assembly data to a read-write area.
		log_debug (LOG_ASSEMBLY, "Copying assembly data to an r/w memory area"sv);

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.start_event (TimingEventKind::AssemblyLoad);
		}

		uint8_t *rw_pointer = static_cast<uint8_t*>(malloc (e.descriptor->data_size));
		memcpy (rw_pointer, e.image_data, e.descriptor->data_size);

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.end_event (true /* uses more info */);

			dynamic_local_string<SENSIBLE_TYPE_NAME_LENGTH> msg;
			msg.append (name);
			msg.append (" (memcpy to r/w area, part of assembly load time)"sv);
			internal_timing.add_more_info (msg);
		}

		set_assembly_data_and_size (rw_pointer, e.descriptor->data_size, assembly_data, assembly_data_size);
		// HACK! END
		// 	set_assembly_data_and_size (e.image_data, e.descriptor->data_size, assembly_data, assembly_data_size);
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
	log_debug (LOG_ASSEMBLY, "AssemblyStore::open_assembly: looking for bundled name: '{}' (hash {:x})"sv, optional_string (name.data ()), name_hash);

	if constexpr (Constants::is_debug_build) {
		// TODO: implement filesystem lookup here

		// In fastdev mode we might not have any assembly store.
		if (assembly_store_hashes == nullptr) {
			log_warn (LOG_ASSEMBLY, "Assembly store not registered. Unable to look up assembly '{}'"sv, name);
			return nullptr;
		}
	}

	const AssemblyStoreIndexEntry *hash_entry = find_assembly_store_entry (name_hash, assembly_store_hashes, assembly_store.index_entry_count);
	if (hash_entry == nullptr) {
		// This message should really be `log_warn`, but since CoreCLR attempts to load `AssemblyName.ni.dll` for each
		// `AssemblyName.dll`, it creates a lot of non-actionable noise.
		// TODO (in separate PR): generate hashes for the .ni.dll names and ignore them at the top of the function. Then restore
		// `log_warn` here.
		log_debug (LOG_ASSEMBLY, "Assembly '{}' (hash 0x{:x}) not found"sv, optional_string (name.data ()), name_hash);
		return nullptr;
	}

	if (hash_entry->descriptor_index >= assembly_store.assembly_count) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Invalid assembly descriptor index {}, exceeds the maximum value of {}"sv,
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
			"Mapped: image_data == {:p}; debug_info_data == {:p}; config_data == {:p}; descriptor == {:p}; data size == {}; debug data size == {}; config data size == {}; name == '{}'"sv,
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
	log_debug (LOG_ASSEMBLY, "Adjusted assembly store pointer: {:p}; size: {}"sv, payload_start, payload_size);
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
				"Assembly store '{}' is not a valid .NET for Android assembly store file"sv,
				get_full_store_path ()
			)
		);
	}

	if (header->version != ASSEMBLY_STORE_FORMAT_VERSION) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Assembly store '{}' uses format version {:x}, instead of the expected {:x}"sv,
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

	log_debug (LOG_ASSEMBLY, "Mapped assembly store {}"sv, get_full_store_path ());
}

#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <cstring>
#include <string_view>

#include <xamarin-app.hh>
#include <host/assembly-store.hh>
#include <runtime-base/crc32.hh>
#include <runtime-base/util.hh>
#include <runtime-base/search.hh>
#include <runtime-base/startup-aware-lock.hh>
#include <runtime-base/timing-internal.hh>
#include <runtime-base/zstd.hh>

using namespace xamarin::android;

AssemblyStoreRuntimeData assembly_store {};

namespace {
	// The assembly store index contains two entries per assembly: one hashed from the name with its
	// file extension (e.g. `Foo.dll`) and one from the name without it (e.g. `Foo`). The names section,
	// however, stores only the full name, so a requested name matches a stored name if it is either
	// equal to it or equal to it with the final extension removed.
	[[gnu::always_inline]]
	auto name_matches (std::string_view const& requested, std::string_view const& stored) noexcept -> bool
	{
		if (requested == stored) {
			return true;
		}

		size_t last_slash = stored.find_last_of ('/');
		size_t name_start = last_slash == std::string_view::npos ? 0 : last_slash + 1;
		size_t last_dot = stored.find_last_of ('.');
		if (last_dot != std::string_view::npos && last_dot > name_start) {
			return requested == stored.substr (0, last_dot);
		}

		return false;
	}
} // anonymous namespace
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

#if defined (RELEASE)
	auto header = reinterpret_cast<const CompressedAssemblyHeader*>(e.image_data);
	if (header->magic == COMPRESSED_DATA_MAGIC) {
		log_debugf (LOG_ASSEMBLY, "Resolving compressed assembly '%.*s' from the assembly store", static_cast<int>(name.length ()), name.data ());

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.start_event (TimingEventKind::AssemblyDecompression);
		}

		if (compressed_count == 0) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Compressed assembly found but no descriptor defined"sv);
		}
		if (header->descriptor_index >= compressed_count) [[unlikely]] {
			Helpers::abort_applicationf (
				LOG_ASSEMBLY,
				std::source_location::current (),
				"Invalid compressed assembly descriptor index %u",
				header->descriptor_index
			);
		}

		CompressedAssemblyDescriptor &cad = compressed_descriptors[header->descriptor_index];
		assembly_data_size = e.descriptor->data_size - sizeof(CompressedAssemblyHeader);

		if (cad.buffer_offset >= uncompressed_size) [[unlikely]] {
			Helpers::abort_applicationf (
				LOG_ASSEMBLY,
				std::source_location::current (),
				"Invalid compressed assembly buffer offset %u. Must be smaller than %u",
				cad.buffer_offset,
				uncompressed_size
			);
		}

		// This is not a perfect check, since we might be still within the buffer size and yet
		// have the tail end of this assembly's data overwritten by the next assembly's data, but
		// that will cause the app to crash when one or the the other assembly is loaded, so it's
		// OK to accept that risk. The whole situation is very, very unlikely.
		if (cad.uncompressed_file_size > uncompressed_size - cad.buffer_offset) [[unlikely]] {
			Helpers::abort_applicationf (
				LOG_ASSEMBLY,
				std::source_location::current (),
				"Invalid compressed assembly buffer size %u at offset %u. Must not exceed %u",
				cad.uncompressed_file_size,
				cad.buffer_offset,
				uncompressed_size - cad.buffer_offset
			);
		}

		uint8_t *data_buffer = uncompressed_buffer + cad.buffer_offset;
		auto is_loaded = [&cad]() noexcept -> bool {
			return __atomic_load_n (&cad.loaded, __ATOMIC_ACQUIRE);
		};

		if (!is_loaded ()) {
			StartupAwareLock decompress_lock (assembly_decompress_mutex);

			if (is_loaded ()) {
				set_assembly_data_and_size (data_buffer, cad.uncompressed_file_size, assembly_data, assembly_data_size);

				if (FastTiming::enabled ()) [[unlikely]] {
					internal_timing.end_event (true /* uses_more_info */);
					internal_timing.add_more_info (name, " (decompressed in another thread)"sv);
				}
				return {assembly_data, assembly_data_size};
			}

			if (header->uncompressed_length != cad.uncompressed_file_size) {
				if (header->uncompressed_length > cad.uncompressed_file_size) {
					Helpers::abort_applicationf (
						LOG_ASSEMBLY,
						std::source_location::current (),
						"Compressed assembly '%.*s' is larger than when the application was built (expected at most %u, got %u). Assemblies don't grow just like that!",
						static_cast<int>(name.length ()),
						name.data (),
						cad.uncompressed_file_size,
						header->uncompressed_length
					);
				} else {
					log_debugf (LOG_ASSEMBLY, "Compressed assembly '%.*s' is smaller than when the application was built. Adjusting accordingly.", static_cast<int>(name.length ()), name.data ());
				}
				cad.uncompressed_file_size = header->uncompressed_length;
			}

			const char *data_start = pointer_add<const char*>(e.image_data, sizeof(CompressedAssemblyHeader));
			log_debugf (LOG_ASSEMBLY, "Decompressing assembly '%.*s' from the assembly store", static_cast<int>(name.length ()), name.data ());
			size_t ret = ZSTD_decompress (data_buffer, cad.uncompressed_file_size, data_start, assembly_data_size);

			if (ZSTD_isError (ret)) {
				Helpers::abort_applicationf (
					LOG_ASSEMBLY,
					std::source_location::current (),
					"Decompression of assembly %.*s failed: %s",
					static_cast<int>(name.length ()),
					name.data (),
					ZSTD_getErrorName (ret)
				);
			}

			if (ret != cad.uncompressed_file_size) {
				Helpers::abort_applicationf (
					LOG_ASSEMBLY,
					std::source_location::current (),
					"Decompression of assembly %.*s yielded a different size (expected %u, got %u)",
					static_cast<int>(name.length ()),
					name.data (),
					cad.uncompressed_file_size,
					static_cast<uint32_t>(ret)
				);
			}

			__atomic_store_n (&cad.loaded, true, __ATOMIC_RELEASE);
			if (FastTiming::enabled ()) [[unlikely]] {
				internal_timing.end_event (true /* uses_more_info */);
				internal_timing.add_more_info (name);
			}
		}

		set_assembly_data_and_size (data_buffer, cad.uncompressed_file_size, assembly_data, assembly_data_size);
	} else
#endif // def RELEASE
	{
		log_debugf (LOG_ASSEMBLY, "Assembly '%.*s' is not compressed in the assembly store", static_cast<int>(name.length ()), name.data ());

		// HACK! START
		// Currently, MAUI crashes when we return a pointer to read-only data, so we must copy
		// the assembly data to a read-write area.
		log_debugf (LOG_ASSEMBLY, "Copying assembly data to an r/w memory area");

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.start_event (TimingEventKind::AssemblyLoad);
		}

		uint8_t *rw_pointer = static_cast<uint8_t*>(malloc (e.descriptor->data_size));
		memcpy (rw_pointer, e.image_data, e.descriptor->data_size);

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.end_event (true /* uses more info */);
			internal_timing.add_more_info (name, " (memcpy to r/w area, part of assembly load time)"sv);
		}

		set_assembly_data_and_size (rw_pointer, e.descriptor->data_size, assembly_data, assembly_data_size);
		// HACK! END
		// 	set_assembly_data_and_size (e.image_data, e.descriptor->data_size, assembly_data, assembly_data_size);
	}

	return {assembly_data, assembly_data_size};
}

[[gnu::always_inline]]
auto AssemblyStore::find_assembly_store_entry (std::string_view const& name, hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept -> const AssemblyStoreIndexEntry*
{
	// Entries are sorted by `name_hash`, so all entries sharing `hash` are contiguous. CRC32 is a
	// 32-bit hash, so collisions are possible (though very unlikely); walk the entire run of entries
	// with a matching hash and compare the actual assembly name to find the correct one.
	auto less_than = [](AssemblyStoreIndexEntry const& entry, hash_t key) -> bool { return entry.name_hash < key; };
	size_t idx = Search::lower_bound<AssemblyStoreIndexEntry, hash_t, less_than> (hash, entries, entry_count);

	while (idx < entry_count && entries[idx].name_hash == hash) {
		AssemblyStoreIndexEntry const& entry = entries[idx];
		if (entry.descriptor_index < assembly_store.assembly_count &&
		    name_matches (name, assembly_store_names[entry.descriptor_index])) {
			return &entry;
		}
		idx++;
	}

	return nullptr;
}

auto AssemblyStore::open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*
{
	hash_t name_hash = crc32_hash (name);

	if constexpr (Constants::is_debug_build) {
		// In fastdev mode we might not have any assembly store.
		if (assembly_store_hashes == nullptr) {
			log_warnf (LOG_ASSEMBLY, "Assembly store not registered. Unable to look up assembly '%.*s'", static_cast<int>(name.length ()), name.data ());
			return nullptr;
		}
	}

	const AssemblyStoreIndexEntry *hash_entry = find_assembly_store_entry (name, name_hash, assembly_store_hashes, assembly_store.index_entry_count);
	if (hash_entry == nullptr) [[unlikely]] {
		size = 0;
		log_warnf (LOG_ASSEMBLY, "Assembly '%.*s' (hash 0x%x) not found", static_cast<int>(name.length ()), name.data (), name_hash);
		return nullptr;
	}

	if (hash_entry->ignore != 0) {
		size = 0;
		log_debugf (LOG_ASSEMBLY, "Assembly '%.*s' ignored", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}

	if (hash_entry->descriptor_index >= assembly_store.assembly_count) {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"Invalid assembly descriptor index %u, exceeds the maximum value of %u",
			hash_entry->descriptor_index,
			assembly_store.assembly_count - 1
		);
	}

	const AssemblyStoreEntryDescriptor &store_entry = assembly_store.assemblies[hash_entry->descriptor_index];
	if (store_entry.mapping_index >= assembly_store.assembly_count) {
		Helpers::abort_application (LOG_ASSEMBLY, "Invalid assembly store runtime mapping index");
	}
	AssemblyStoreSingleAssemblyRuntimeData &assembly_runtime_info = runtime_assemblies[store_entry.mapping_index];

	if (assembly_runtime_info.image_data == nullptr) {
		// The assignments here don't need to be atomic, the value will always be the same, so even if two threads
		// arrive here at the same time, nothing bad will happen.
		assembly_runtime_info.image_data = assembly_store.data_start + store_entry.data_offset;
		assembly_runtime_info.descriptor = &store_entry;
		if (store_entry.debug_data_offset != 0) {
			assembly_runtime_info.debug_info_data = assembly_store.data_start + store_entry.debug_data_offset;
		}

		log_debugf (
			LOG_ASSEMBLY,
			"Mapped: image_data == %p; debug_info_data == %p; config_data == %p; descriptor == %p; data size == %u; debug data size == %u; config data size == %u; name == '%.*s'",
			static_cast<const void*>(assembly_runtime_info.image_data),
			static_cast<const void*>(assembly_runtime_info.debug_info_data),
			static_cast<const void*>(assembly_runtime_info.config_data),
			static_cast<const void*>(assembly_runtime_info.descriptor),
			assembly_runtime_info.descriptor->data_size,
			assembly_runtime_info.descriptor->debug_data_size,
			assembly_runtime_info.descriptor->config_data_size,
			static_cast<int>(name.length ()),
			name.data ()
		);
	}

	auto [assembly_data, assembly_data_size] = get_assembly_data (assembly_runtime_info, name);
	size = assembly_data_size;
	return assembly_data;
}

void AssemblyStore::configure_from_payload (const void *payload_start, const char *store_path) noexcept
{
	auto header = static_cast<const AssemblyStoreHeader*>(payload_start);

	if (header->magic != ASSEMBLY_STORE_MAGIC) {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"Assembly store '%s' is not a valid .NET for Android assembly store file",
			optional_string (store_path)
		);
	}

	if (header->version != ASSEMBLY_STORE_FORMAT_VERSION) {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"Assembly store '%s' uses format version %x, instead of the expected %x",
			optional_string (store_path),
			header->version,
			ASSEMBLY_STORE_FORMAT_VERSION
		);
	}

	constexpr size_t header_size = sizeof(AssemblyStoreHeader);
	size_t index_size = Helpers::multiply_with_overflow_check<size_t> (header->index_entry_count, sizeof (AssemblyStoreIndexEntry));
	size_t descriptor_size = Helpers::multiply_with_overflow_check<size_t> (header->entry_count, sizeof (AssemblyStoreEntryDescriptor));
	if (header->index_size != index_size) {
		Helpers::abort_application (LOG_ASSEMBLY, "Invalid assembly store index size");
	}
	size_t descriptors_offset = Helpers::add_with_overflow_check<size_t> (header_size, index_size);
	size_t names_offset = Helpers::add_with_overflow_check<size_t> (descriptors_offset, descriptor_size);

	assembly_store.data_start = static_cast<const uint8_t*>(payload_start);
	assembly_store.assembly_count = header->entry_count;
	assembly_store.index_entry_count = header->index_entry_count;
	assembly_store.assemblies = reinterpret_cast<const AssemblyStoreEntryDescriptor*>(assembly_store.data_start + descriptors_offset);
	assembly_store_hashes = reinterpret_cast<const AssemblyStoreIndexEntry*>(assembly_store.data_start + header_size);

	// Build a lookup of assembly names indexed by descriptor index, used to disambiguate CRC32 hash
	// collisions during lookup. The names section follows the descriptor table and consists of
	// `entry_count` length-prefixed (uint32 length followed by the UTF-8 bytes) records, stored in
	// descriptor-index order. The `free` guards against a leak should the (single) store ever be
	// re-mapped; `assembly_store_names` is nullptr on first call, for which it is a no-op.
	const uint8_t *names_cursor = assembly_store.data_start + names_offset;
	std::free (assembly_store_names);
	assembly_store_names = header->entry_count == 0 ? nullptr :
		static_cast<std::string_view*>(std::calloc (header->entry_count, sizeof (std::string_view)));
	if (header->entry_count != 0 && assembly_store_names == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate memory for the assembly store name table");
	}

	for (uint32_t i = 0; i < header->entry_count; i++) {
		uint32_t name_length;
		memcpy (&name_length, names_cursor, sizeof (name_length));
		names_cursor += sizeof (name_length);
		assembly_store_names[i] = std::string_view (reinterpret_cast<const char*>(names_cursor), name_length);
		names_cursor += name_length;
	}

	if (runtime_assemblies != nullptr || compressed_descriptors != nullptr) {
		Helpers::abort_application (LOG_ASSEMBLY, "Assembly store was already configured");
	}
	if (header->entry_count != 0) {
		runtime_assemblies = static_cast<AssemblyStoreSingleAssemblyRuntimeData*> (
			std::calloc (header->entry_count, sizeof (AssemblyStoreSingleAssemblyRuntimeData))
		);
		if (runtime_assemblies == nullptr) {
			Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate assembly store runtime data");
		}
	}

#if defined (RELEASE)
	uint32_t found_compressed_count = 0;
	for (uint32_t i = 0; i < header->entry_count; i++) {
		const AssemblyStoreEntryDescriptor &entry = assembly_store.assemblies[i];
		if (entry.data_size == 0) {
			continue;
		}
		if (entry.mapping_index >= header->entry_count) {
			Helpers::abort_application (LOG_ASSEMBLY, "Invalid assembly store runtime mapping index");
		}
		if (entry.data_size < sizeof (CompressedAssemblyHeader)) {
			continue;
		}

		CompressedAssemblyHeader compressed_header;
		std::memcpy (&compressed_header, assembly_store.data_start + entry.data_offset, sizeof (compressed_header));
		if (compressed_header.magic == COMPRESSED_DATA_MAGIC) {
			if (compressed_header.descriptor_index >= header->entry_count || compressed_header.uncompressed_length == 0) {
				Helpers::abort_application (LOG_ASSEMBLY, "Invalid compressed assembly descriptor");
			}
			found_compressed_count = std::max (found_compressed_count, compressed_header.descriptor_index + 1);
		}
	}

	compressed_count = found_compressed_count;
	if (compressed_count != 0) {
		compressed_descriptors = static_cast<CompressedAssemblyDescriptor*> (
			std::calloc (compressed_count, sizeof (CompressedAssemblyDescriptor))
		);
		if (compressed_descriptors == nullptr) {
			Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate compressed assembly descriptors");
		}

		for (uint32_t i = 0; i < header->entry_count; i++) {
			const AssemblyStoreEntryDescriptor &entry = assembly_store.assemblies[i];
			if (entry.data_size < sizeof (CompressedAssemblyHeader)) {
				continue;
			}
			CompressedAssemblyHeader compressed_header;
			std::memcpy (&compressed_header, assembly_store.data_start + entry.data_offset, sizeof (compressed_header));
			if (compressed_header.magic != COMPRESSED_DATA_MAGIC) {
				continue;
			}
			CompressedAssemblyDescriptor &descriptor = compressed_descriptors[compressed_header.descriptor_index];
			if (descriptor.uncompressed_file_size != 0) {
				Helpers::abort_application (LOG_ASSEMBLY, "Duplicate compressed assembly descriptor");
			}
			descriptor.uncompressed_file_size = compressed_header.uncompressed_length;
		}

		size_t buffer_size = 0;
		for (uint32_t i = 0; i < compressed_count; i++) {
			CompressedAssemblyDescriptor &descriptor = compressed_descriptors[i];
			if (descriptor.uncompressed_file_size != 0) {
				if (buffer_size > UINT32_MAX) {
					Helpers::abort_application (LOG_ASSEMBLY, "Compressed assembly buffers exceed the supported size");
				}
				descriptor.buffer_offset = static_cast<uint32_t>(buffer_size);
				buffer_size = Helpers::add_with_overflow_check<size_t> (buffer_size, descriptor.uncompressed_file_size);
			}
		}
		if (buffer_size > UINT32_MAX) {
			Helpers::abort_application (LOG_ASSEMBLY, "Compressed assembly buffers exceed the supported size");
		}
		uncompressed_size = static_cast<uint32_t>(buffer_size);
		uncompressed_buffer = static_cast<uint8_t*>(std::calloc (buffer_size, 1));
		if (uncompressed_buffer == nullptr) {
			Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate assembly decompression buffer");
		}
	}
#endif // defined (RELEASE)

	log_debugf (LOG_ASSEMBLY, "Mapped assembly store %s", optional_string (store_path));
}

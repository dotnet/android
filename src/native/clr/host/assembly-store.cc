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
#include <runtime-base/zstd.hh>

using namespace xamarin::android;

namespace {
#if defined (RELEASE)
	auto read_compressed_header (const uint8_t *data, uint32_t size, CompressedAssemblyHeader &header) noexcept -> bool
	{
		if (size < sizeof (header)) {
			return false;
		}

		// Store data follows variable-length names and need not be naturally aligned.
		std::memcpy (&header, data, sizeof (header));
		return header.magic == COMPRESSED_DATA_MAGIC;
	}
#endif

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
	CompressedAssemblyHeader header;
	if (read_compressed_header (e.image_data, e.descriptor->data_size, header)) {
		log_debugf (LOG_ASSEMBLY, "Resolving compressed assembly '%.*s' from the assembly store", static_cast<int>(name.length ()), name.data ());

		if (compressed_count == 0) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Compressed assembly found but no descriptor defined"sv);
		}
		if (header.descriptor_index >= compressed_count) [[unlikely]] {
			Helpers::abort_applicationf (
				LOG_ASSEMBLY,
				std::source_location::current (),
				"Invalid compressed assembly descriptor index %u",
				header.descriptor_index
			);
		}

		CompressedAssemblyDescriptor &cad = compressed_descriptors[header.descriptor_index];
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
				return {assembly_data, assembly_data_size};
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

		uint8_t *rw_pointer = static_cast<uint8_t*>(malloc (e.descriptor->data_size));
		if (rw_pointer == nullptr) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate writable assembly data");
		}
		memcpy (rw_pointer, e.image_data, e.descriptor->data_size);

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
			log_debugf (LOG_ASSEMBLY, "Skipping assembly store lookup for '%.*s': no assembly store is registered (normal with FastDev)", static_cast<int>(name.length ()), name.data ());
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
	if (store_entry.data_size == 0) {
		size = 0;
		log_debugf (LOG_ASSEMBLY, "Assembly '%.*s' has no data in the assembly store", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}
	AssemblyStoreSingleAssemblyRuntimeData const& assembly_runtime_info = runtime_assemblies[store_entry.mapping_index];

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
	size_t descriptors_offset = Helpers::add_with_overflow_check<size_t> (header_size, header->index_size);
	size_t descriptors_size = Helpers::multiply_with_overflow_check<size_t> (header->entry_count, sizeof (AssemblyStoreEntryDescriptor));
	size_t names_offset = Helpers::add_with_overflow_check<size_t> (descriptors_offset, descriptors_size);

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
	size_t names_size = Helpers::multiply_with_overflow_check<size_t> (header->entry_count, sizeof (std::string_view));
	assembly_store_names = names_size == 0 ? nullptr : static_cast<std::string_view*>(std::calloc (1, names_size));
	if (names_size != 0 && assembly_store_names == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate memory for the assembly store name table");
	}

	for (uint32_t i = 0; i < header->entry_count; i++) {
		uint32_t name_length;
		memcpy (&name_length, names_cursor, sizeof (name_length));
		names_cursor += sizeof (name_length);
		assembly_store_names[i] = std::string_view (reinterpret_cast<const char*>(names_cursor), name_length);
		names_cursor += name_length;
	}

	std::free (runtime_assemblies);
	size_t runtime_size = Helpers::multiply_with_overflow_check<size_t> (header->entry_count, sizeof (AssemblyStoreSingleAssemblyRuntimeData));
	runtime_assemblies = runtime_size == 0 ? nullptr :
		static_cast<AssemblyStoreSingleAssemblyRuntimeData*>(std::calloc (1, runtime_size));
	if (runtime_size != 0 && runtime_assemblies == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate assembly store runtime data");
	}

	std::free (compressed_descriptors);
	compressed_descriptors = nullptr;
	compressed_count = 0;
	uncompressed_size = 0;
	// CoreCLR can retain returned assembly data for the lifetime of the app. If the store is
	// reconfigured, keep the previous decompression buffer alive, like the mapped payload itself.
	uncompressed_buffer = nullptr;

	for (uint32_t i = 0; i < header->entry_count; i++) {
		const AssemblyStoreEntryDescriptor &entry = assembly_store.assemblies[i];
		if (entry.data_size == 0) {
			continue;
		}
		if (entry.mapping_index >= header->entry_count) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Invalid assembly store runtime mapping index");
		}

		// Populate pointers during configuration, before probes can run concurrently.
		AssemblyStoreSingleAssemblyRuntimeData &runtime = runtime_assemblies[entry.mapping_index];
		runtime.image_data = assembly_store.data_start + entry.data_offset;
		runtime.descriptor = &entry;
		if (entry.debug_data_offset != 0) {
			runtime.debug_info_data = assembly_store.data_start + entry.debug_data_offset;
		}
		if (entry.config_data_offset != 0) {
			runtime.config_data = assembly_store.data_start + entry.config_data_offset;
		}

#if defined (RELEASE)
		CompressedAssemblyHeader compressed_header;
		if (read_compressed_header (runtime.image_data, entry.data_size, compressed_header)) {
			if (compressed_header.uncompressed_length == 0) [[unlikely]] {
				Helpers::abort_application (LOG_ASSEMBLY, "Invalid compressed assembly length");
			}
			// Compression indices come from the pre-trimming assembly list. They can have
			// gaps, and the largest index can exceed this store's entry_count.
			uint32_t capacity = Helpers::add_with_overflow_check<uint32_t> (compressed_header.descriptor_index, 1u);
			compressed_count = std::max (compressed_count, capacity);
		}
#endif
	}

#if defined (RELEASE)
	if (compressed_count != 0) {
		size_t compressed_size = Helpers::multiply_with_overflow_check<size_t> (compressed_count, sizeof (CompressedAssemblyDescriptor));
		compressed_descriptors = static_cast<CompressedAssemblyDescriptor*>(std::calloc (1, compressed_size));
		if (compressed_descriptors == nullptr) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate compressed assembly descriptors");
		}

		for (uint32_t i = 0; i < header->entry_count; i++) {
			const AssemblyStoreEntryDescriptor &entry = assembly_store.assemblies[i];
			CompressedAssemblyHeader compressed_header;
			if (!read_compressed_header (assembly_store.data_start + entry.data_offset, entry.data_size, compressed_header)) {
				continue;
			}

			CompressedAssemblyDescriptor &descriptor = compressed_descriptors[compressed_header.descriptor_index];
			if (descriptor.uncompressed_file_size != 0) [[unlikely]] {
				Helpers::abort_application (LOG_ASSEMBLY, "Duplicate compressed assembly descriptor index");
			}
			descriptor.uncompressed_file_size = compressed_header.uncompressed_length;
			descriptor.buffer_offset = uncompressed_size;
			uncompressed_size = Helpers::add_with_overflow_check<uint32_t> (uncompressed_size, compressed_header.uncompressed_length);
		}

		uncompressed_buffer = static_cast<uint8_t*>(std::calloc (uncompressed_size, 1));
		if (uncompressed_buffer == nullptr) [[unlikely]] {
			Helpers::abort_application (LOG_ASSEMBLY, "Unable to allocate assembly decompression buffer");
		}
	}
#endif

	log_debugf (LOG_ASSEMBLY, "Assembly store runtime data: %u entries, %u compressed descriptor slots, %u bytes for decompression",
		assembly_store.assembly_count, compressed_count, uncompressed_size);
	log_debugf (LOG_ASSEMBLY, "Mapped assembly store %s", optional_string (store_path));
}

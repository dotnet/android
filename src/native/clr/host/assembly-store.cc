#include <fcntl.h>
#include <unistd.h>
#include <sys/stat.h>

#include <cerrno>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#if defined (HAVE_LZ4)
#include <lz4.h>
#endif

#include <zlib.h>

#include <xamarin-app.hh>
#include <constants.hh>
#include <host/assembly-store.hh>
#include <startup/zip.hh>
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
		// EXPERIMENT (assemblystore-mmap zero-copy): the assembly is stored uncompressed in a store
		// mmap'd MAP_PRIVATE, PROT_READ|PROT_WRITE. Hand CoreCLR a pointer straight into the mapping
		// with no decompression and no memcpy: in-place writes to the PE image fault in copy-on-write
		// pages (only the touched ones), everything else stays demand-paged and shared with the file's
		// page cache. Requires the store to page-align each assembly (see AssemblyStoreGenerator) so
		// one image's writes cannot corrupt a neighbour sharing a page.
		log_debug (LOG_ASSEMBLY, "assemblystore-mmap: zero-copy mapping assembly '{}' ({} bytes)"sv, name, e.descriptor->data_size);

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.start_event (TimingEventKind::AssemblyLoad);
		}

		set_assembly_data_and_size (e.image_data, e.descriptor->data_size, assembly_data, assembly_data_size);

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.end_event (true /* uses more info */);

			dynamic_local_string<SENSIBLE_TYPE_NAME_LENGTH> msg;
			msg.append (name);
			msg.append (" (zero-copy mmap COW pointer)"sv);
			internal_timing.add_more_info (msg);
		}
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

void AssemblyStore::register_extra_assembly (std::string_view const& name, int fd, std::string_view const& apk_path, uint32_t offset, uint32_t size) noexcept
{
	detail::mmap_info map = Util::mmap_file (fd, offset, size, apk_path);
	auto [payload_start, payload_size] = Util::get_wrapper_dso_payload_pointer_and_size (map, apk_path);

	// CoreCLR needs a writable image (see the get_assembly_data HACK), so copy the payload into a
	// read-write buffer that lives for the process lifetime.
	uint8_t *rw = static_cast<uint8_t*>(malloc (payload_size));
	memcpy (rw, payload_start, payload_size);

	hash_t name_hash = xxhash::hash (name.data (), name.length ());
	extra_assemblies.push_back (ExtraAssembly { name_hash, rw, static_cast<uint32_t>(payload_size) });

	log_debug (
		LOG_ASSEMBLY,
		"assemblystore-mmap: registered extra assembly '{}' (hash 0x{:x}, {} bytes) from '{}'"sv,
		name, name_hash, payload_size, apk_path
	);
}

auto AssemblyStore::open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*
{
	hash_t name_hash = xxhash::hash (name.data (), name.length ());

	// EXPERIMENT (assemblystore-mmap hybrid): out-of-store assemblies (R2R composites) live in
	// per-ABI lib/<abi>/*.so entries instead of the shared store; serve them here.
	for (ExtraAssembly const& extra : extra_assemblies) {
		if (extra.name_hash == name_hash) {
			size = extra.size;
			log_debug (LOG_ASSEMBLY, "assemblystore-mmap: serving extra assembly '{}' ({} bytes)"sv, name, extra.size);
			return extra.data;
		}
	}

	if constexpr (Constants::is_debug_build) {
		// In fastdev mode we might not have any assembly store.
		if (assembly_store_hashes == nullptr) {
			log_warn (LOG_ASSEMBLY, "Assembly store not registered. Unable to look up assembly '{}'"sv, name);
			return nullptr;
		}
	}

	const AssemblyStoreIndexEntry *hash_entry = find_assembly_store_entry (name_hash, assembly_store_hashes, assembly_store.index_entry_count);
	if (hash_entry == nullptr) [[unlikely]] {
		size = 0;
		log_warn (LOG_ASSEMBLY, "Assembly '{}' (hash 0x{:x}) not found"sv, name, name_hash);
		return nullptr;
	}

	if (hash_entry->ignore != 0) {
		size = 0;
		log_debug (LOG_ASSEMBLY, "Assembly '{}' ignored"sv, name);
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

void AssemblyStore::map (int fd, std::string_view const& apk_path, std::string_view const& store_path, uint32_t offset, uint32_t size, bool writable) noexcept
{
	detail::mmap_info assembly_store_map = Util::mmap_file (fd, offset, size, store_path, writable);

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

// EXPERIMENT (assemblystore-mmap): the assembly store is shipped as a plain, DEFLATE-compressed
// APK asset (`assemblies/<abi>/libassembly-store.assemblystore`) rather than a native library, so
// that Google Play keeps it compressed for AAB delivery. It cannot be mmap'd straight from the APK
// (compressed ZIP entries are not contiguous uncompressed bytes), so on the first launch we inflate
// it once into the app's files dir and, on every launch, mmap that uncompressed on-disk copy with
// no per-assembly decompression.
bool AssemblyStore::try_extract_and_map (std::string_view const& apk_path, std::string_view const& override_dir) noexcept
{
	int apk_fd = open (apk_path.data (), O_RDONLY);
	if (apk_fd < 0) {
		log_warn (LOG_ASSEMBLY, "assemblystore-mmap: unable to open APK '{}': {}"sv, apk_path, std::strerror (errno));
		return false;
	}

	std::string entry_name;
	entry_name.append ("assets/"sv);
	entry_name.append (Constants::assembly_store_asset_file_name);

	Zip::zip_entry_info info {};
	if (!Zip::find_entry (apk_fd, apk_path, entry_name, info)) {
		close (apk_fd);
		return false;
	}

	log_debug (
		LOG_ASSEMBLY,
		"assemblystore-mmap: found store asset '{}' (offset {}, {} compressed, {} uncompressed, method {})"sv,
		entry_name, info.data_offset, info.compressed_size, info.uncompressed_size, info.compression_method
	);

	std::string dest;
	dest.append (override_dir);
	dest.append ("/"sv);
	dest.append (Constants::assembly_store_asset_file_name);

	// Extract once: reuse an already-extracted copy of the expected size.
	struct stat st;
	bool need_extract = !(stat (dest.c_str (), &st) == 0 && static_cast<uint32_t>(st.st_size) == info.uncompressed_size);

	if (need_extract) {
		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.start_event (TimingEventKind::AssemblyDecompression);
		}

		// The override/files sub-directory may not exist yet; create it recursively.
		{
			std::string partial;
			for (char c : dest) {
				if (c == '/' && !partial.empty ()) {
					mkdir (partial.c_str (), 0700); // best-effort; EEXIST is fine
				}
				partial += c;
			}
		}

		std::vector<uint8_t> compressed (info.compressed_size);
		size_t total = 0;
		while (total < info.compressed_size) {
			ssize_t n = pread (apk_fd, compressed.data () + total, info.compressed_size - total, static_cast<off_t>(info.data_offset + total));
			if (n < 0) {
				if (errno == EINTR) {
					continue;
				}
				Helpers::abort_application (LOG_ASSEMBLY, std::format ("assemblystore-mmap: failed to read store data from APK: {}", std::strerror (errno)));
			}
			if (n == 0) {
				break;
			}
			total += static_cast<size_t>(n);
		}

		std::string tmp = dest;
		tmp.append (".tmp"sv);
		int out_fd = open (tmp.c_str (), O_WRONLY | O_CREAT | O_TRUNC, 0600);
		if (out_fd < 0) {
			Helpers::abort_application (LOG_ASSEMBLY, std::format ("assemblystore-mmap: unable to create '{}': {}", tmp, std::strerror (errno)));
		}

		auto write_all = [](int fd, const uint8_t *data, size_t len) -> bool {
			size_t off = 0;
			while (off < len) {
				ssize_t w = write (fd, data + off, len - off);
				if (w < 0) {
					if (errno == EINTR) {
						continue;
					}
					return false;
				}
				off += static_cast<size_t>(w);
			}
			return true;
		};

		bool ok = true;
		if (info.compression_method == 0) {
			ok = write_all (out_fd, compressed.data (), info.compressed_size);
		} else {
			z_stream zs {};
			if (inflateInit2 (&zs, -MAX_WBITS) != Z_OK) {
				Helpers::abort_application (LOG_ASSEMBLY, "assemblystore-mmap: inflateInit2 failed"sv);
			}
			zs.next_in = compressed.data ();
			zs.avail_in = info.compressed_size;
			std::vector<uint8_t> outbuf (256uz * 1024uz);
			int ret;
			do {
				zs.next_out = outbuf.data ();
				zs.avail_out = static_cast<uInt>(outbuf.size ());
				ret = inflate (&zs, Z_NO_FLUSH);
				if (ret != Z_OK && ret != Z_STREAM_END) {
					inflateEnd (&zs);
					Helpers::abort_application (LOG_ASSEMBLY, std::format ("assemblystore-mmap: inflate failed with code {}", ret));
				}
				size_t have = outbuf.size () - zs.avail_out;
				if (!write_all (out_fd, outbuf.data (), have)) {
					ok = false;
					break;
				}
			} while (ret != Z_STREAM_END);
			inflateEnd (&zs);
		}

		if (!ok) {
			Helpers::abort_application (LOG_ASSEMBLY, std::format ("assemblystore-mmap: failed to write '{}': {}", tmp, std::strerror (errno)));
		}

		fsync (out_fd);
		close (out_fd);
		if (rename (tmp.c_str (), dest.c_str ()) != 0) {
			Helpers::abort_application (LOG_ASSEMBLY, std::format ("assemblystore-mmap: failed to rename '{}' to '{}': {}", tmp, dest, std::strerror (errno)));
		}

		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.end_event (true /* uses_more_info */);
			internal_timing.add_more_info ("assemblystore-mmap extract"sv);
		}
		log_debug (LOG_ASSEMBLY, "assemblystore-mmap: extracted store to '{}' ({} bytes)"sv, dest, info.uncompressed_size);
	} else {
		log_debug (LOG_ASSEMBLY, "assemblystore-mmap: reusing extracted store '{}'"sv, dest);
	}

	close (apk_fd);

	int store_fd = open (dest.c_str (), O_RDONLY);
	if (store_fd < 0) {
		Helpers::abort_application (LOG_ASSEMBLY, std::format ("assemblystore-mmap: unable to open extracted store '{}': {}", dest, std::strerror (errno)));
	}
	AssemblyStore::map (store_fd, dest, 0, info.uncompressed_size, /* writable */ true);
	close (store_fd);
	return true;
}

#include <cerrno>
#include <cstdlib>
#include <condition_variable>
#include <deque>
#include <string>
#include <thread>

#include <fcntl.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

#include <xamarin-app.hh>
#include <host/assembly-store.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>
#include <runtime-base/search.hh>
#include <runtime-base/startup-aware-lock.hh>
#include <runtime-base/timing-internal.hh>
#include <runtime-base/zstd.hh>

using namespace xamarin::android;

namespace {
	// -------------------------------------------------------------------------
	// EXPERIMENTAL: on-device cache of decompressed assemblies.
	//
	// The first time a compressed assembly is decompressed we queue its
	// uncompressed bytes to be written (on a single background thread) to a
	// per-assembly file in the app cache directory. On subsequent launches the
	// file is mmap'd directly, skipping decompression entirely: no ZSTD cost,
	// and the pages are file-backed / copy-on-write rather than dirty anonymous
	// memory.
	//
	// This is a prototype:
	//   * There is no MSBuild opt-in yet (always on in RELEASE on this branch;
	//     set the XA_DISABLE_ASSEMBLY_CACHE env var to turn it off for A/B runs).
	//   * There is no assembly-store version stamp yet. Per-assembly staleness
	//     is guarded by an 8-byte footer holding a hash of the *compressed*
	//     payload, so any change to an assembly (e.g. after an app update)
	//     invalidates just that assembly's cache file.
	//   * We only ever cache the assemblies that were actually touched, since
	//     decompression is lazy and driven by the runtime's assembly probe.
	// -------------------------------------------------------------------------
	namespace asm_cache {
		constexpr std::string_view CACHE_DIR_NAME = "decompressed-assembly-cache"sv;
		constexpr size_t FOOTER_SIZE = sizeof (uint64_t);

		struct WriteRequest final
		{
			std::string    path;
			const uint8_t *data; // stable pointer into uncompressed_assemblies_data_buffer
			size_t         size;
			uint64_t       token;
		};

		std::mutex               state_lock;
		std::condition_variable  queue_cv;
		std::deque<WriteRequest> write_queue;
		std::string              cache_dir;
		bool                     initialized = false;
		bool                     enabled = false;
		bool                     writer_running = false;

		// Runtime-only, per-compressed-assembly pointer to the resolved
		// uncompressed data (either the mmap'd cache file or the shared
		// decompression buffer). Indexed by CompressedAssemblyHeader.descriptor_index.
		uint8_t **tracking = nullptr;

		[[gnu::cold]]
		void writer_loop () noexcept
		{
			for (;;) {
				WriteRequest req;
				{
					std::unique_lock lock (state_lock);
					queue_cv.wait (lock, [] { return !write_queue.empty (); });
					req = std::move (write_queue.front ());
					write_queue.pop_front ();
				}

				std::string tmp_path = req.path;
				tmp_path.append (".tmp"sv);

				int fd = open (tmp_path.c_str (), O_WRONLY | O_CREAT | O_TRUNC, 0600);
				if (fd < 0) {
					continue;
				}

				bool ok = true;
				size_t off = 0;
				while (off < req.size) {
					ssize_t written = write (fd, req.data + off, req.size - off);
					if (written < 0) {
						if (errno == EINTR) {
							continue;
						}
						ok = false;
						break;
					}
					off += static_cast<size_t>(written);
				}

				if (ok) {
					ssize_t written = write (fd, &req.token, FOOTER_SIZE);
					ok = (written == static_cast<ssize_t>(FOOTER_SIZE));
				}

				if (ok) {
					fsync (fd);
				}
				close (fd);

				// Atomic publish: a half-written temp file never becomes visible
				// under the final name.
				if (!ok || rename (tmp_path.c_str (), req.path.c_str ()) != 0) {
					unlink (tmp_path.c_str ());
				}
			}
		}

		// Must be called while holding assembly_decompress_mutex.
		void ensure_initialized () noexcept
		{
			if (initialized) {
				return;
			}
			initialized = true;

			// Allow disabling at runtime (without rebuilding) for A/B benchmarking:
			//   adb shell setprop debug.net.asmcache 0   # off
			//   adb shell setprop debug.net.asmcache 1   # on (default)
			if (getenv ("XA_DISABLE_ASSEMBLY_CACHE") != nullptr) {
				return;
			}
			{
				dynamic_local_property_string prop_value;
				if (AndroidSystem::monodroid_get_system_property ("debug.net.asmcache"sv, prop_value) > 0 &&
				    prop_value.get () != nullptr && prop_value.get ()[0] == '0') {
					log_debug (LOG_ASSEMBLY, "On-device decompressed-assembly cache disabled via debug.net.asmcache"sv);
					return;
				}
			}

			// The app code-cache directory (Context.getCodeCacheDir()) is wiped by
			// Android on both app and platform updates, so a stale cache from a
			// previous build cannot survive an update.
			std::string const& code_cache_dir = AndroidSystem::get_app_code_cache_dir ();
			if (code_cache_dir.empty ()) {
				return;
			}

			cache_dir.assign (code_cache_dir);
			cache_dir.append ("/");
			cache_dir.append (CACHE_DIR_NAME);
			mkdir (cache_dir.c_str (), 0700); // ignore errors (e.g. EEXIST)

			if (compressed_assembly_count > 0) {
				tracking = new (std::nothrow) uint8_t*[compressed_assembly_count]();
			}

			enabled = (tracking != nullptr);
		}

		auto build_path (std::string_view name) noexcept -> std::string
		{
			std::string path = cache_dir;
			path.append ("/");
			path.append (name);
			return path;
		}

		// Attempts to mmap a previously cached, decompressed assembly. Returns
		// nullptr on any miss (absent, wrong size, stale footer, mmap failure).
		// Must be called while holding assembly_decompress_mutex.
		auto try_load (std::string_view name, uint32_t expected_size, uint64_t token) noexcept -> uint8_t*
		{
			if (!enabled) {
				return nullptr;
			}

			std::string path = build_path (name);
			int fd = open (path.c_str (), O_RDONLY);
			if (fd < 0) {
				return nullptr;
			}

			struct stat st {};
			if (fstat (fd, &st) != 0 ||
			    static_cast<uint64_t>(st.st_size) != static_cast<uint64_t>(expected_size) + FOOTER_SIZE) {
				close (fd);
				return nullptr;
			}

			size_t map_size = static_cast<size_t>(expected_size) + FOOTER_SIZE;
			// PROT_WRITE + MAP_PRIVATE: copy-on-write so the CLR/MAUI can write
			// into the image (see the r/w HACK in the uncompressed path) while
			// pages stay clean/file-backed until actually modified.
			void *mapped = mmap (nullptr, map_size, PROT_READ | PROT_WRITE, MAP_PRIVATE, fd, 0);
			close (fd);
			if (mapped == MAP_FAILED) {
				return nullptr;
			}

			uint64_t stored_token = 0;
			memcpy (&stored_token, static_cast<uint8_t*>(mapped) + expected_size, FOOTER_SIZE);
			if (stored_token != token) {
				munmap (mapped, map_size);
				return nullptr;
			}

			return static_cast<uint8_t*>(mapped);
		}

		// Queues a freshly decompressed assembly to be persisted. The data
		// pointer must remain valid and immutable for the process lifetime
		// (it points into uncompressed_assemblies_data_buffer).
		// Must be called while holding assembly_decompress_mutex.
		void enqueue_write (std::string_view name, const uint8_t *data, size_t size, uint64_t token) noexcept
		{
			if (!enabled) {
				return;
			}

			WriteRequest req {
				.path  = build_path (name),
				.data  = data,
				.size  = size,
				.token = token,
			};

			{
				std::lock_guard lock (state_lock);
				write_queue.push_back (std::move (req));
				if (!writer_running) {
					writer_running = true;
					std::thread (writer_loop).detach ();
				}
			}
			queue_cv.notify_one ();
		}
	} // namespace asm_cache
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
		uint32_t const descriptor_index = header->descriptor_index;

		// Resolves to the mmap'd cache file when this assembly was loaded from
		// the on-device cache, otherwise to the shared decompression buffer.
		auto resolve_data = [descriptor_index, data_buffer]() noexcept -> uint8_t* {
			if (asm_cache::tracking != nullptr && asm_cache::tracking[descriptor_index] != nullptr) {
				return asm_cache::tracking[descriptor_index];
			}
			return data_buffer;
		};

		if (!cad.loaded) {
			StartupAwareLock decompress_lock (assembly_decompress_mutex);

			if (cad.loaded) {
				set_assembly_data_and_size (resolve_data (), cad.uncompressed_file_size, assembly_data, assembly_data_size);

				if (FastTiming::enabled ()) [[unlikely]] {
					internal_timing.end_event (true /* uses_more_info */);

					dynamic_local_string<SENSIBLE_TYPE_NAME_LENGTH> msg;
					msg.append (name);
					msg.append (" (decompressed in another thread)"sv);
					internal_timing.add_more_info (msg);
				}
				return {assembly_data, assembly_data_size};
			}

			asm_cache::ensure_initialized ();

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
			uint64_t payload_token = static_cast<uint64_t>(xxhash::hash (data_start, assembly_data_size));

			uint8_t *cached = asm_cache::try_load (name, cad.uncompressed_file_size, payload_token);
			if (cached != nullptr) {
				log_debug (LOG_ASSEMBLY, "Loaded decompressed assembly '{}' from the on-device cache"sv, name);
				if (asm_cache::tracking != nullptr) {
					asm_cache::tracking[descriptor_index] = cached;
				}
			} else {
				size_t ret = ZSTD_decompress (data_buffer, cad.uncompressed_file_size, data_start, assembly_data_size);

				if (ZSTD_isError (ret)) {
					Helpers::abort_application (
						LOG_ASSEMBLY,
						std::format (
							"Decompression of assembly {} failed: {}"sv,
							name,
							ZSTD_getErrorName (ret)
						)
					);
				}

				if (ret != cad.uncompressed_file_size) {
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

				asm_cache::enqueue_write (name, data_buffer, cad.uncompressed_file_size, payload_token);
			}

			cad.loaded = true;
			if (FastTiming::enabled ()) [[unlikely]] {
				internal_timing.end_event (true /* uses_more_info */);
				internal_timing.add_more_info (name);
			}
		}

		set_assembly_data_and_size (resolve_data (), cad.uncompressed_file_size, assembly_data, assembly_data_size);
	} else
#endif // def RELEASE
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

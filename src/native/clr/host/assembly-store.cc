#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <memory>
#include <mutex>
#include <string>

#include <fcntl.h>
#include <pthread.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

#include <xamarin-app.hh>
#include <host/assembly-store.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/crc32.hh>
#include <runtime-base/util.hh>
#include <runtime-base/search.hh>
#include <runtime-base/startup-aware-lock.hh>
#include <runtime-base/timing-internal.hh>
#include <runtime-base/zstd.hh>

using namespace xamarin::android;

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

	namespace asm_cache {
		constexpr std::string_view CACHE_DIR_NAME = "decompressed-assembly-cache-v1"sv;
		constexpr uint32_t CACHE_FILE_MAGIC = 0x43434158; // 'XACC', little-endian
		constexpr uint32_t CACHE_FILE_FORMAT_VERSION = 1;
		constexpr size_t MAX_QUEUED_BYTES = 32uz * 1024uz * 1024uz;

		struct [[gnu::packed]] CacheFileFooter final
		{
			uint32_t magic;
			uint32_t version;
			uint64_t store_id;
			uint64_t payload_hash;
			uint32_t descriptor_index;
			uint32_t payload_size;
		};

		static_assert (sizeof (CacheFileFooter) == 32uz);

		struct WriteRequest final
		{
			std::string                path;
			std::unique_ptr<uint8_t[]> data;
			size_t                     size;
		};

		enum class WriteResult
		{
			Succeeded,
			Failed,
		};

		std::mutex                        state_lock;
		std::deque<WriteRequest>          write_queue;
		std::string                       cache_dir;
		std::unique_ptr<uint8_t*[]>       tracking;
		size_t                            queued_bytes = 0;
		uint64_t                          store_id = 0;
		bool                              initialized = false;
		bool                              enabled = false;
		bool                              writes_enabled = false;
		bool                              writer_running = false;

		auto hash_payload (const uint8_t *data, size_t size) noexcept -> uint64_t
		{
			return static_cast<uint64_t>(crc32_hash (reinterpret_cast<const char*>(data), size));
		}

		bool write_fully (int fd, const uint8_t *buf, size_t len) noexcept
		{
			size_t off = 0;
			while (off < len) {
				ssize_t n = write (fd, buf + off, len - off);
				if (n < 0) {
					if (errno == EINTR) {
						continue;
					}
					return false;
				}
				if (n == 0) {
					errno = EIO;
					return false;
				}
				off += static_cast<size_t>(n);
			}
			return true;
		}

		void log_file_error (std::string_view operation, std::string const& path, int error) noexcept
		{
			log_debug (LOG_ASSEMBLY, "Decompressed-assembly cache {} failed for '{}': {}"sv, operation, path, std::strerror (error));
		}

		auto write_cache_file (WriteRequest const& req) noexcept -> WriteResult
		{
			std::string tmp_path = req.path;
			tmp_path.append (".tmp."sv);
			tmp_path.append (std::to_string (getpid ()));

			int fd;
			do {
				fd = open (tmp_path.c_str (), O_WRONLY | O_CREAT | O_TRUNC | O_CLOEXEC | O_NOFOLLOW, 0600);
			} while (fd < 0 && errno == EINTR);
			if (fd < 0) {
				log_file_error ("temporary-file creation"sv, req.path, errno);
				return WriteResult::Failed;
			}

			bool ok = write_fully (fd, req.data.get (), req.size);
			int error = ok ? 0 : errno;
			if (close (fd) != 0 && ok) {
				ok = false;
				error = errno;
			}

			if (!ok) {
				log_file_error ("write"sv, req.path, error);
				unlink (tmp_path.c_str ());
				return WriteResult::Failed;
			}

			int rename_result;
			do {
				rename_result = rename (tmp_path.c_str (), req.path.c_str ());
			} while (rename_result != 0 && errno == EINTR);

			if (rename_result != 0) {
				error = errno;
				log_file_error ("publish"sv, req.path, error);
				unlink (tmp_path.c_str ());
				return WriteResult::Failed;
			}

			return WriteResult::Succeeded;
		}

		void clear_write_queue_locked () noexcept
		{
			for (WriteRequest const& request : write_queue) {
				queued_bytes -= request.size;
			}
			write_queue.clear ();
		}

		[[gnu::cold]]
		auto writer_loop ([[maybe_unused]] void *arg) noexcept -> void*
		{
			while (true) {
				WriteRequest request;
				{
					std::lock_guard lock (state_lock);
					if (write_queue.empty ()) {
						writer_running = false;
						return nullptr;
					}

					request = std::move (write_queue.front ());
					write_queue.pop_front ();
				}

				size_t request_size = request.size;
				WriteResult write_result = write_cache_file (request);
				request.data.reset ();

				{
					std::lock_guard lock (state_lock);
					queued_bytes -= request_size;
					if (write_result == WriteResult::Failed) {
						writes_enabled = false;
						clear_write_queue_locked ();
						writer_running = false;
						log_debug (LOG_ASSEMBLY, "Disabling decompressed-assembly cache writes after a persistence failure"sv);
						return nullptr;
					}
				}
			}
		}

		bool start_writer_locked () noexcept
		{
			pthread_attr_t attributes;
			int result = pthread_attr_init (&attributes);
			bool attributes_initialized = result == 0;
			if (result == 0) {
				result = pthread_attr_setdetachstate (&attributes, PTHREAD_CREATE_DETACHED);
			}

			pthread_t writer_thread;
			if (result == 0) {
				result = pthread_create (&writer_thread, &attributes, writer_loop, nullptr);
			}

			if (attributes_initialized) {
				pthread_attr_destroy (&attributes);
			}
			if (result != 0) {
				log_debug (LOG_ASSEMBLY, "Failed to start decompressed-assembly cache writer: {}"sv, std::strerror (result));
				return false;
			}

			return true;
		}

		bool ensure_directory (std::string const& path) noexcept
		{
			if (mkdir (path.c_str (), 0700) == 0) {
				return true;
			}

			int error = errno;
			if (error != EEXIST) {
				log_file_error ("directory creation"sv, path, error);
				return false;
			}

			struct stat st {};
			if (lstat (path.c_str (), &st) != 0) {
				log_file_error ("directory validation"sv, path, errno);
				return false;
			}
			if (!S_ISDIR (st.st_mode)) {
				log_file_error ("directory validation"sv, path, ENOTDIR);
				return false;
			}

			return true;
		}

		void ensure_initialized (uint64_t assembly_store_id) noexcept
		{
			if (initialized) {
				return;
			}
			initialized = true;

			bool cache_requested = application_config.assembly_store_decompression_cache_enabled;

			// Allow overriding the build setting at runtime for A/B benchmarking:
			//   adb shell setprop debug.net.asmcache 0   # off
			//   adb shell setprop debug.net.asmcache 1   # on
			if (getenv ("XA_DISABLE_ASSEMBLY_CACHE") != nullptr) {
				return;
			}
			{
				dynamic_local_property_string prop_value;
				if (AndroidSystem::monodroid_get_system_property ("debug.net.asmcache"sv, prop_value) > 0 && prop_value.get () != nullptr) {
					if (prop_value.get ()[0] == '0') {
						cache_requested = false;
					} else if (prop_value.get ()[0] == '1') {
						cache_requested = true;
					}
				}
			}

			if (!cache_requested) {
				return;
			}

			std::string const& code_cache_dir = AndroidSystem::get_app_code_cache_dir ();
			if (code_cache_dir.empty ()) {
				return;
			}

			cache_dir.assign (code_cache_dir);
			cache_dir.append ("/");
			cache_dir.append (CACHE_DIR_NAME);
			if (!ensure_directory (cache_dir)) {
				return;
			}

			store_id = assembly_store_id;
			cache_dir.append ("/");
			cache_dir.append (std::format ("{:x}", store_id));
			if (!ensure_directory (cache_dir)) {
				return;
			}

			if (compressed_assembly_count > 0) {
				tracking.reset (new (std::nothrow) uint8_t*[compressed_assembly_count]());
			}

			enabled = (tracking != nullptr);
			if (!enabled) {
				return;
			}

			{
				std::lock_guard lock (state_lock);
				writes_enabled = true;
			}

			log_debug (
				LOG_ASSEMBLY,
				"Enabled decompressed-assembly cache at '{}'; store ID 0x{:x}; write queue limit {} bytes"sv,
				cache_dir,
				store_id,
				MAX_QUEUED_BYTES
			);
		}

		auto build_path (uint32_t descriptor_index) noexcept -> std::string
		{
			std::string path = cache_dir;
			path.append ("/");
			path.append (std::to_string (descriptor_index));
			path.append (".bin"sv);
			return path;
		}

		auto try_load (uint32_t descriptor_index, std::string_view name, uint32_t expected_size) noexcept -> uint8_t*
		{
			if (!enabled) {
				return nullptr;
			}

			std::string path = build_path (descriptor_index);
			int fd = open (path.c_str (), O_RDONLY | O_CLOEXEC | O_NOFOLLOW);
			if (fd < 0) {
				return nullptr;
			}

			struct stat st {};
			if (fstat (fd, &st) != 0 ||
			    !S_ISREG (st.st_mode) ||
			    static_cast<uint64_t>(st.st_size) != static_cast<uint64_t>(expected_size) + sizeof (CacheFileFooter)) {
				close (fd);
				return nullptr;
			}

			size_t map_size = static_cast<size_t>(expected_size) + sizeof (CacheFileFooter);
			// The runtime may modify the image, so keep those changes private while
			// retaining clean file-backed pages until they are actually written.
			void *mapped = mmap (nullptr, map_size, PROT_READ | PROT_WRITE, MAP_PRIVATE, fd, 0);
			close (fd);
			if (mapped == MAP_FAILED) {
				return nullptr;
			}

			CacheFileFooter footer {};
			memcpy (&footer, static_cast<uint8_t*>(mapped) + expected_size, sizeof (footer));
			if (footer.magic != CACHE_FILE_MAGIC ||
			    footer.version != CACHE_FILE_FORMAT_VERSION ||
			    footer.store_id != store_id ||
			    footer.descriptor_index != descriptor_index ||
			    footer.payload_size != expected_size ||
			    footer.payload_hash != hash_payload (static_cast<uint8_t*>(mapped), expected_size)) {
				munmap (mapped, map_size);
				log_debug (LOG_ASSEMBLY, "Ignoring invalid decompressed-assembly cache entry for '{}'"sv, name);
				return nullptr;
			}

			return static_cast<uint8_t*>(mapped);
		}

		void enqueue_write (uint32_t descriptor_index, std::string_view name, const uint8_t *data, size_t size) noexcept
		{
			if (!enabled) {
				return;
			}

			if (size > SIZE_MAX - sizeof (CacheFileFooter)) {
				return;
			}
			size_t total = size + sizeof (CacheFileFooter);

			size_t bytes_queued = 0;
			bool queue_full = false;
			{
				std::lock_guard lock (state_lock);
				if (!writes_enabled) {
					return;
				}
				if (total > MAX_QUEUED_BYTES || queued_bytes > MAX_QUEUED_BYTES - total) {
					queue_full = true;
					bytes_queued = queued_bytes;
				} else {
					queued_bytes += total;
				}
			}

			if (queue_full) {
				if (total > MAX_QUEUED_BYTES) {
					log_debug (
						LOG_ASSEMBLY,
						"Skipping decompressed-assembly cache write for '{}': {} bytes exceed the {}-byte queue limit"sv,
						name,
						total,
						MAX_QUEUED_BYTES
					);
				} else {
					log_debug (
						LOG_ASSEMBLY,
						"Skipping decompressed-assembly cache write for '{}': {} of {} queue bytes are in use"sv,
						name,
						bytes_queued,
						MAX_QUEUED_BYTES
					);
				}
				return;
			}

			auto snapshot = std::unique_ptr<uint8_t[]> (new (std::nothrow) uint8_t[total]);
			if (snapshot == nullptr) {
				std::lock_guard lock (state_lock);
				queued_bytes -= total;
				return;
			}
			// The runtime can modify the shared decompression buffer after this
			// method returns, so the background writer needs an immutable copy.
			memcpy (snapshot.get (), data, size);

			CacheFileFooter footer {
				.magic = CACHE_FILE_MAGIC,
				.version = CACHE_FILE_FORMAT_VERSION,
				.store_id = store_id,
				.payload_hash = hash_payload (snapshot.get (), size),
				.descriptor_index = descriptor_index,
				.payload_size = static_cast<uint32_t>(size),
			};
			memcpy (snapshot.get () + size, &footer, sizeof (footer));

			WriteRequest req {
				.path = build_path (descriptor_index),
				.data = std::move (snapshot),
				.size = total,
			};

			{
				std::lock_guard lock (state_lock);
				if (!writes_enabled) {
					queued_bytes -= total;
					return;
				}

				write_queue.push_back (std::move (req));
				if (!writer_running) {
					writer_running = true;
					if (!start_writer_locked ()) {
						writer_running = false;
						writes_enabled = false;
						clear_write_queue_locked ();
					}
				}
			}
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
		log_debug (LOG_ASSEMBLY, "Resolving compressed assembly '{}' from the assembly store"sv, name);

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
		auto is_loaded = [&cad]() noexcept -> bool {
			return __atomic_load_n (&cad.loaded, __ATOMIC_ACQUIRE);
		};

		// Resolves to the mmap'd cache file when this assembly was loaded from
		// the on-device cache, otherwise to the shared decompression buffer.
		auto resolve_data = [descriptor_index, data_buffer]() noexcept -> uint8_t* {
			if (asm_cache::tracking != nullptr && asm_cache::tracking[descriptor_index] != nullptr) {
				return asm_cache::tracking[descriptor_index];
			}
			return data_buffer;
		};

		if (!is_loaded ()) {
			StartupAwareLock decompress_lock (assembly_decompress_mutex);

			if (is_loaded ()) {
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

			asm_cache::ensure_initialized (assembly_store_content_id);

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

			bool loaded_from_cache = false;
			uint8_t *cached = asm_cache::try_load (descriptor_index, name, cad.uncompressed_file_size);
			if (cached != nullptr) {
				loaded_from_cache = true;
				log_debug (LOG_ASSEMBLY, "Loaded decompressed assembly '{}' from the on-device cache"sv, name);
				if (asm_cache::tracking != nullptr) {
					asm_cache::tracking[descriptor_index] = cached;
				}
			} else {
				log_debug (LOG_ASSEMBLY, "Decompressing assembly '{}' from the assembly store"sv, name);
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

				asm_cache::enqueue_write (descriptor_index, name, data_buffer, cad.uncompressed_file_size);
			}

			__atomic_store_n (&cad.loaded, true, __ATOMIC_RELEASE);
			if (FastTiming::enabled ()) [[unlikely]] {
				internal_timing.end_event (true /* uses_more_info */);

				dynamic_local_string<SENSIBLE_TYPE_NAME_LENGTH> msg;
				msg.append (name);
				if (loaded_from_cache) {
					msg.append (" (decompressed cache hit)"sv);
				}
				internal_timing.add_more_info (msg);
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
			log_warn (LOG_ASSEMBLY, "Assembly store not registered. Unable to look up assembly '{}'"sv, name);
			return nullptr;
		}
	}

	const AssemblyStoreIndexEntry *hash_entry = find_assembly_store_entry (name, name_hash, assembly_store_hashes, assembly_store.index_entry_count);
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

	const AssemblyStoreEntryDescriptor &store_entry = assembly_store.assemblies[hash_entry->descriptor_index];
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
			static_cast<const void*>(assembly_runtime_info.image_data),
			static_cast<const void*>(assembly_runtime_info.debug_info_data),
			static_cast<const void*>(assembly_runtime_info.config_data),
			static_cast<const void*>(assembly_runtime_info.descriptor),
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

void AssemblyStore::configure_from_payload (const void *payload_start, const std::function<std::string()>& get_full_store_path) noexcept
{
	auto header = static_cast<const AssemblyStoreHeader*>(payload_start);

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

	assembly_store_content_id = header->content_id;
	assembly_store.data_start = static_cast<const uint8_t*>(payload_start);
	assembly_store.assembly_count = header->entry_count;
	assembly_store.index_entry_count = header->index_entry_count;
	assembly_store.assemblies = reinterpret_cast<const AssemblyStoreEntryDescriptor*>(assembly_store.data_start + header_size + header->index_size);
	assembly_store_hashes = reinterpret_cast<const AssemblyStoreIndexEntry*>(assembly_store.data_start + header_size);

	// Build a lookup of assembly names indexed by descriptor index, used to disambiguate CRC32 hash
	// collisions during lookup. The names section follows the descriptor table and consists of
	// `entry_count` length-prefixed (uint32 length followed by the UTF-8 bytes) records, stored in
	// descriptor-index order. `delete[]` guards against a leak should the (single) store ever be
	// re-mapped; `assembly_store_names` is nullptr on first call, for which it is a no-op.
	const uint8_t *names_cursor = assembly_store.data_start + header_size + header->index_size +
		(static_cast<size_t>(header->entry_count) * sizeof (AssemblyStoreEntryDescriptor));
	delete[] assembly_store_names;
	assembly_store_names = new std::string_view[header->entry_count];
	for (uint32_t i = 0; i < header->entry_count; i++) {
		uint32_t name_length;
		memcpy (&name_length, names_cursor, sizeof (name_length));
		names_cursor += sizeof (name_length);
		assembly_store_names[i] = std::string_view (reinterpret_cast<const char*>(names_cursor), name_length);
		names_cursor += name_length;
	}

	log_debug (LOG_ASSEMBLY, "Mapped assembly store {}; content ID 0x{:x}"sv, get_full_store_path (), assembly_store_content_id);
}

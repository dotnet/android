#pragma once

#include <string_view>

#include <dlfcn.h>

#include <runtime-base/mutex.hh>

#include "../xamarin-app.hh"

#include "android-system.hh"
#include "coreclr-bootstrap.hh"
#include <runtime-base/crc32.hh>
#include <runtime-base/dso-loader.hh>
#include <runtime-base/search.hh>
#include "startup-aware-lock.hh"

namespace xamarin::android
{
	class MonodroidDl
	{
		struct ActiveLoadFrame
		{
			uint32_t name_index;
			ActiveLoadFrame *parent;
		};

		static inline pthread_mutex_t dso_handle_write_lock = PTHREAD_MUTEX_INITIALIZER;
		static inline thread_local ActiveLoadFrame *active_load_stack = nullptr;

		static auto is_same_thread_loading (uint32_t name_index) noexcept -> bool
		{
			for (ActiveLoadFrame *cur = active_load_stack; cur != nullptr; cur = cur->parent) {
				if (cur->name_index == name_index) {
					return true;
				}
			}

			return false;
		}

		static void publish_completed_handle (DSOCacheEntry *dso, void *handle) noexcept
		{
			uint32_t name_index = dso->name_index;
			for (size_t i = 0; i < CoreClrBootstrap::config.number_of_dso_cache_entries; i++) {
				DSOCacheEntry &entry = CoreClrBootstrap::dso_cache[i];
				if (entry.name_index == name_index) {
					__atomic_store_n (&entry.handle, handle, __ATOMIC_RELEASE);
				}
			}
			__atomic_store_n (&dso->handle, handle, __ATOMIC_RELEASE);
		}

		static auto load_dso (DSOCacheEntry *dso, std::string_view const& name, int flags) noexcept -> void*
		{
			std::string_view dso_name = get_dso_name (dso);
			void *handle = AndroidSystem::load_dso_from_any_directories (dso_name, flags, dso->is_jni_library);
			if (handle == nullptr) {
				handle = AndroidSystem::load_dso_from_any_directories (name, flags, dso->is_jni_library);
			}
			return handle;
		}

		// Entries are sorted by `hash`, so all entries sharing `hash` are contiguous. CRC32 is a
		// 32-bit hash, so collisions are possible (though very unlikely); walk the whole run of
		// entries with a matching hash and compare the exact build-generated alias before accepting it.
		[[gnu::always_inline, gnu::flatten]]
		static auto find_dso_cache_entry (std::string_view const& name, hash_t hash) noexcept -> DSOCacheEntry*
		{
			log_debugf (LOG_ASSEMBLY, "Looking for hash %x in DSO cache", hash);

			auto less_than = [](DSOCacheEntry const& entry, hash_t key) -> bool { return entry.hash < key; };
			size_t idx = Search::lower_bound<DSOCacheEntry, hash_t, less_than> (hash, CoreClrBootstrap::dso_cache, CoreClrBootstrap::config.number_of_dso_cache_entries);

			while (idx < CoreClrBootstrap::config.number_of_dso_cache_entries && CoreClrBootstrap::dso_cache[idx].hash == hash) {
				DSOCacheEntry &entry = CoreClrBootstrap::dso_cache[idx];
				if (name == CoreClrBootstrap::string (entry.alias_index, false)) {
					return &entry;
				}
				idx++;
			}

			return nullptr;
		}

	public:
		[[gnu::always_inline]]
		static auto get_dso_name (const DSOCacheEntry *const dso) -> std::string_view
		{
			if (dso == nullptr) {
				return "<unknown>"sv;
			}

			return CoreClrBootstrap::string (dso->name_index);
		}

		static auto monodroid_dlopen (DSOCacheEntry *dso, std::string_view const& name, int flags) noexcept -> void*
		{
			auto dso_name_for_log = get_dso_name (dso);
			log_debugf (LOG_ASSEMBLY, "monodroid_dlopen: hash match %sfound, DSO name is '%.*s'", dso == nullptr ? "not " : "", static_cast<int>(dso_name_for_log.length ()), dso_name_for_log.data ());

			if (dso == nullptr) {
				// Packaged libraries have build-time JNI metadata in the DSO cache.  A cache miss is therefore
				// assumed to be a platform or other non-JNI library, avoiding an unnecessary JNI System.loadLibrary
				// call.
				constexpr bool IsJniLibrary = false;
				constexpr bool SkipExistsCheck = true;
				return DsoLoader::load<SkipExistsCheck> (name, flags, IsJniLibrary);
			}

			void *cached_handle = __atomic_load_n (&dso->handle, __ATOMIC_ACQUIRE);
			if (cached_handle != nullptr) {
				log_debugf (LOG_ASSEMBLY, "monodroid_dlopen: library %.*s already loaded, returning handle %p", static_cast<int>(name.length ()), name.data (), cached_handle);
				return cached_handle;
			}

			if (dso->ignore) {
				auto ignored_dso_name = get_dso_name (dso);
				log_infof (LOG_ASSEMBLY, "Request to load '%.*s' ignored, it is known not to exist", static_cast<int>(ignored_dso_name.length ()), ignored_dso_name.data ());
				return nullptr;
			}

			// Perform the actual load outside dso_handle_write_lock so synchronous JNI_OnLoad callbacks
			// can re-enter managed code and resolve other P/Invokes without deadlocking on the cache mutex.
			// Do not block cross-thread callers on a pre-Java in-progress gate: Android's Runtime.loadLibrary0
			// is synchronized on the Runtime monitor and ART's SharedLibrary::CheckOnLoadResult coordinates
			// concurrent/recursive JNI_OnLoad execution internally. Track only the current thread's active
			// load stack so a same-thread recursive (A -> A) call during JNI_OnLoad returns its provisional
			// handle without publishing it to dso->handle before the outermost load succeeds.
			if (is_same_thread_loading (dso->name_index)) {
				return load_dso (dso, name, flags);
			}

			ActiveLoadFrame frame {
				.name_index = dso->name_index,
				.parent = active_load_stack,
			};
			active_load_stack = &frame;
			void *handle = load_dso (dso, name, flags);
			active_load_stack = frame.parent;
			if (handle == nullptr) {
				return nullptr;
			}

			StartupAwareLock lock (dso_handle_write_lock);
			cached_handle = __atomic_load_n (&dso->handle, __ATOMIC_RELAXED);
			if (cached_handle != nullptr) {
				return cached_handle;
			}

			publish_completed_handle (dso, handle);
			return handle;
		}

		static auto monodroid_dlopen (std::string_view const& name, int flags) noexcept -> void*
		{
			if (name.empty ()) [[unlikely]] {
				log_warnf (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+");
				return nullptr;
			}

			hash_t name_hash = crc32_hash (name);
			log_debugf (LOG_ASSEMBLY, "monodroid_dlopen: hash for name '%.*s' is %x", static_cast<int>(name.length ()), name.data (), name_hash);

			DSOCacheEntry *dso = find_dso_cache_entry (name, name_hash);
			return monodroid_dlopen (dso, name, flags);
		}
	};
}

#pragma once

#include <string_view>

#include <dlfcn.h>
#include <android/dlext.h>

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
		static inline pthread_mutex_t dso_handle_write_lock = PTHREAD_MUTEX_INITIALIZER;

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
				// assumed to be a platform or other non-JNI library, avoiding a synchronous System.loadLibrary
				// dispatch to the main thread.
				constexpr bool IsJniLibrary = false;
				constexpr bool SkipExistsCheck = true;
				return DsoLoader::load<SkipExistsCheck> (name, flags, IsJniLibrary);
			} else if (dso->handle != nullptr) {
				log_debugf (LOG_ASSEMBLY, "monodroid_dlopen: library %.*s already loaded, returning handle %p", static_cast<int>(name.length ()), name.data (), dso->handle);
				return dso->handle;
			}

			if (dso->ignore) {
				auto ignored_dso_name = get_dso_name (dso);
				log_infof (LOG_ASSEMBLY, "Request to load '%.*s' ignored, it is known not to exist", static_cast<int>(ignored_dso_name.length ()), ignored_dso_name.data ());
				return nullptr;
			}

			std::string_view dso_name = get_dso_name (dso);
			StartupAwareLock lock (dso_handle_write_lock);
			dso->handle = AndroidSystem::load_dso_from_any_directories (dso_name, flags, dso->is_jni_library);

			if (dso->handle != nullptr) {
				return dso->handle;
			}

			dso->handle = AndroidSystem::load_dso_from_any_directories (name, flags, dso->is_jni_library);
			return dso->handle;
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

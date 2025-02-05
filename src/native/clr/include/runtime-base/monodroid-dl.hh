#pragma once

#include <mutex>

#include <dlfcn.h>
#include <android/dlext.h>

#include <java-interop-dlfcn.h>

#include "../shared/xxhash.hh"
#include "../xamarin-app.hh"

#include "android-system.hh"
#include "search.hh"
#include "startup-aware-lock.hh"

namespace xamarin::android
{
	class MonodroidDl
	{
		enum class CacheKind
		{
			// Access AOT cache
			AOT,

			// Access DSO cache
			DSO,
		};

		static inline std::mutex   dso_handle_write_lock;

		template<CacheKind WhichCache>
		[[gnu::always_inline, gnu::flatten]]
		static auto find_dso_cache_entry_common (hash_t hash) noexcept -> DSOCacheEntry*
		{
			static_assert (WhichCache == CacheKind::AOT || WhichCache == CacheKind::DSO, "Unknown cache type specified");

			DSOCacheEntry *arr;
			size_t arr_size;

			if constexpr (WhichCache == CacheKind::AOT) {
				log_debug (LOG_ASSEMBLY, "Looking for hash {:x} in AOT cache", hash);
				arr = aot_dso_cache;
				arr_size = application_config.number_of_aot_cache_entries;
			} else if constexpr (WhichCache == CacheKind::DSO) {
				log_debug (LOG_ASSEMBLY, "Looking for hash {:x} in DSO cache", hash);
				arr = dso_cache;
				arr_size = application_config.number_of_dso_cache_entries;
			}

			auto equal = [](DSOCacheEntry const& entry, hash_t key) -> bool { return entry.hash == key; };
			auto less_than = [](DSOCacheEntry const& entry, hash_t key) -> bool { return entry.hash < key; };
			ssize_t idx = Search::binary_search<DSOCacheEntry, equal, less_than> (hash, arr, arr_size);

			if (idx >= 0) {
				return &arr[idx];
			}

			return nullptr;
		}

		[[gnu::always_inline, gnu::flatten]]
		static auto find_only_aot_cache_entry (hash_t hash) noexcept -> DSOCacheEntry*
		{
			return find_dso_cache_entry_common<CacheKind::AOT> (hash);
		}

		[[gnu::always_inline, gnu::flatten]]
		static auto find_only_dso_cache_entry (hash_t hash) noexcept -> DSOCacheEntry*
		{
			return find_dso_cache_entry_common<CacheKind::DSO> (hash);
		}

		static auto monodroid_dlopen_log_and_return (void *handle, const char *full_name) -> void*
		{
			if (handle == nullptr) {
				const char *load_error = dlerror ();
				if (load_error == nullptr) {
					load_error = "Unknown error";
				}
				log_error (
					LOG_ASSEMBLY,
					"Could not load library '{}'. {}",
					full_name,
					load_error
				);
			}

			return handle;
		}

		static auto monodroid_dlopen_ignore_component_or_load (const char *name, int flags) noexcept -> void*
		{
			unsigned int dl_flags = static_cast<unsigned int>(flags);
			void * handle = AndroidSystem::load_dso_from_any_directories (name, dl_flags);
			if (handle != nullptr) {
				return monodroid_dlopen_log_and_return (handle, name);
			}

			handle = AndroidSystem::load_dso (name, dl_flags, false /* skip_existing_check */);
			return monodroid_dlopen_log_and_return (handle, name);
		}

	public:
		[[gnu::flatten]]
		static auto monodroid_dlopen (const char *name, int flags, bool prefer_aot_cache) noexcept -> void*
		{
			if (name == nullptr) {
				log_warn (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+"sv);
				return nullptr;
			}

			hash_t name_hash = xxhash::hash (name, strlen (name));
			log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash for name '{}' is {:x}", name, name_hash);

			DSOCacheEntry *dso = nullptr;
			if (prefer_aot_cache) {
				// If we're asked to look in the AOT DSO cache, do it first.  This is because we're likely called from the
				// MonoVM's dlopen fallback handler and it will not be a request to resolved a p/invoke, but most likely to
				// find and load an AOT image for a managed assembly.  Since there might be naming/hash conflicts in this
				// scenario, we look at the AOT cache first.
				//
				// See: https://github.com/dotnet/android/issues/9081
				dso = find_only_aot_cache_entry (name_hash);
			}

			if (dso == nullptr) {
				dso = find_only_dso_cache_entry (name_hash);
			}

			log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash match {}found, DSO name is '{}'", dso == nullptr ? "not "sv : ""sv, dso == nullptr ? "<unknown>"sv : dso->name);

			if (dso == nullptr) {
				// DSO not known at build time, try to load it
				return monodroid_dlopen_ignore_component_or_load (name, flags);
			} else if (dso->handle != nullptr) {
				return monodroid_dlopen_log_and_return (dso->handle, dso->name);
			}

			if (dso->ignore) {
				log_info (LOG_ASSEMBLY, "Request to load '{}' ignored, it is known not to exist", dso->name);
				return nullptr;
			}

			StartupAwareLock lock (dso_handle_write_lock);
#if defined (RELEASE)
			if (AndroidSystem::is_embedded_dso_mode_enabled ()) {
				DSOApkEntry *apk_entry = dso_apk_entries;
				for (size_t i = 0uz; i < application_config.number_of_shared_libraries; i++) {
					if (apk_entry->name_hash != dso->real_name_hash) {
						apk_entry++;
						continue;
					}

					android_dlextinfo dli;
					dli.flags = ANDROID_DLEXT_USE_LIBRARY_FD | ANDROID_DLEXT_USE_LIBRARY_FD_OFFSET;
					dli.library_fd = apk_entry->fd;
					dli.library_fd_offset = apk_entry->offset;
					dso->handle = android_dlopen_ext (dso->name, flags, &dli);

					if (dso->handle != nullptr) {
						return monodroid_dlopen_log_and_return (dso->handle, dso->name);
					}
					break;
				}
			}
#endif
			unsigned int dl_flags = static_cast<unsigned int>(flags);
			dso->handle = AndroidSystem::load_dso_from_any_directories (dso->name, dl_flags);

			if (dso->handle != nullptr) {
				return monodroid_dlopen_log_and_return (dso->handle, dso->name);
			}

			dso->handle = AndroidSystem::load_dso_from_any_directories (name, dl_flags);
			return monodroid_dlopen_log_and_return (dso->handle, name);
		}

		[[gnu::flatten]]
		static auto monodroid_dlopen (const char *name, int flags) noexcept -> void*
		{
			// We're called by MonoVM via a callback, we might need to return an AOT DSO.
			// See: https://github.com/dotnet/android/issues/9081
			constexpr bool PREFER_AOT_CACHE = true;
			return monodroid_dlopen (name, flags, PREFER_AOT_CACHE);
		}

		[[gnu::flatten]]
		static auto monodroid_dlsym (void *handle, const char *name) -> void*
		{
			char *e = nullptr;
			void *s = microsoft::java_interop::java_interop_lib_symbol (handle, name, &e);

			if (s == nullptr) {
				log_error (
					LOG_ASSEMBLY,
					"Could not find symbol '{}': {}",
					optional_string (name),
					optional_string (e)
				);
			}

			if (e != nullptr) {
				java_interop_free (e);
			}

			return s;
		}
	};
}

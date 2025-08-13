#pragma once

#include <mutex>
#include <string_view>

#include <dlfcn.h>
#include <android/dlext.h>

#include <java-interop-dlfcn.h>

#include <shared/xxhash.hh>
#include "../xamarin-app.hh"

#include "android-system.hh"
#include <runtime-base/dso-loader.hh>
#include <runtime-base/search.hh>
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

		[[gnu::always_inline]]
		static auto get_dso_name (const DSOCacheEntry *const dso) -> std::string_view
		{
			if (dso == nullptr) {
				return "<unknown>"sv;
			}

			return &dso_names_data[dso->name_index];
		}

	public:
		[[gnu::flatten]]
		static auto monodroid_dlopen (DSOCacheEntry *dso, std::string_view const& name, int flags) noexcept -> void*
		{
			log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash match {}found, DSO name is '{}'", dso == nullptr ? "not "sv : ""sv, get_dso_name (dso));

			if (dso == nullptr) {
				// DSO not known at build time, try to load it. Since we don't know whether or not the library uses
				// JNI, we're going to assume it does and thus use System.loadLibrary eventually.
				return DsoLoader::load (name, flags, true /* is_jni */);
			} else if (dso->handle != nullptr) {
				log_debug (LOG_ASSEMBLY, "monodroid_dlopen: library {} already loaded, returning handle {:p}", name, dso->handle);
				return dso->handle;
			}

			if (dso->ignore) {
				log_info (LOG_ASSEMBLY, "Request to load '{}' ignored, it is known not to exist", get_dso_name (dso));
				return nullptr;
			}

			std::string_view dso_name = get_dso_name (dso);
			StartupAwareLock lock (dso_handle_write_lock);
#if defined (RELEASE)
			if (AndroidSystem::is_embedded_dso_mode_enabled ()) {
				DSOApkEntry *apk_entry = dso_apk_entries;
				for (size_t i = 0uz; i < application_config.number_of_shared_libraries; i++) {
					if (apk_entry->name_hash != dso->real_name_hash) {
						apk_entry++;
						continue;
					}

					dso->handle = DsoLoader::load (apk_entry->fd, apk_entry->offset, dso_name, flags, dso->is_jni_library);
					if (dso->handle != nullptr) {
						return dso->handle;
					}
					break;
				}
			}
#endif
			dso->handle = AndroidSystem::load_dso_from_any_directories (dso_name, flags, dso->is_jni_library);

			if (dso->handle != nullptr) {
				return dso->handle;
			}

			dso->handle = AndroidSystem::load_dso_from_any_directories (name, flags, dso->is_jni_library);
			return dso->handle;
		}

		template<bool PREFER_AOT_CACHE> [[gnu::flatten]]
		static auto monodroid_dlopen (std::string_view const& name, int flags) noexcept -> void*
		{
			if (name.empty ()) [[unlikely]] {
				log_warn (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+"sv);
				return nullptr;
			}

			hash_t name_hash = xxhash::hash (name.data (), name.size ());
			log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash for name '{}' is {:x}", name, name_hash);

			DSOCacheEntry *dso = nullptr;
			if constexpr (PREFER_AOT_CACHE) {
				// This code isn't currently used by CoreCLR, but it's possible that in the future we will have separate
				// .so files for AOT-d assemblies, similar to MonoVM, so let's keep it.
				//
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

			return monodroid_dlopen (dso, name, flags);
		}

		[[gnu::flatten]]
		static auto monodroid_dlopen (const char *name, int flags) noexcept -> void*
		{
			// We're called by MonoVM via a callback, we might need to return an AOT DSO.
			// See: https://github.com/dotnet/android/issues/9081
			constexpr bool PREFER_AOT_CACHE = true;
			return monodroid_dlopen<PREFER_AOT_CACHE> (name, flags);
		}

		[[gnu::flatten]]
		static auto monodroid_dlsym (void *handle, std::string_view const& name) -> void*
		{
			char *e = nullptr;
			void *s = microsoft::java_interop::java_interop_lib_symbol (handle, name.data (), &e);

			if (s == nullptr) {
				log_error (
					LOG_ASSEMBLY,
					"Could not find symbol '{}': {}",
					name,
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

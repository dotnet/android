#pragma once

#include <mutex>
#include <string_view>

#include <dlfcn.h>
#include <android/dlext.h>

#include <java-interop-dlfcn.h>

#include "../xamarin-app.hh"

#include "android-system.hh"
#include <runtime-base/crc32.hh>
#include <runtime-base/dso-loader.hh>
#include <runtime-base/search.hh>
#include "startup-aware-lock.hh"

namespace xamarin::android
{
	class MonodroidDl
	{
		static inline std::mutex   dso_handle_write_lock;

		[[gnu::always_inline, gnu::flatten]]
		static auto find_dso_cache_entry (hash_t hash) noexcept -> DSOCacheEntry*
		{
			log_debug (LOG_ASSEMBLY, "Looking for hash {:x} in DSO cache", hash);

			auto equal = [](DSOCacheEntry const& entry, hash_t key) -> bool { return entry.hash == key; };
			auto less_than = [](DSOCacheEntry const& entry, hash_t key) -> bool { return entry.hash < key; };
			ssize_t idx = Search::binary_search<DSOCacheEntry, hash_t, equal, less_than> (hash, dso_cache, application_config.number_of_dso_cache_entries);

			if (idx >= 0) {
				return &dso_cache[idx];
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

			return &dso_names_data[dso->name_index];
		}

		[[gnu::flatten]]
		static auto find_dso_apk_entry (hash_t hash) -> DSOApkEntry*
		{
			auto equal = [](DSOApkEntry const& entry, hash_t key) -> bool { return entry.name_hash == key; };
			auto less_than = [](DSOApkEntry const& entry, hash_t key) -> bool { return entry.name_hash < key; };
			ssize_t idx = Search::binary_search<DSOApkEntry, hash_t, equal, less_than> (
				hash,
				dso_apk_entries, application_config.number_of_shared_libraries
			);

			if (idx >= 0) [[likely]] {
				return &dso_apk_entries[idx];
			}

			return nullptr;
		}

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
				DSOApkEntry *apk_entry = find_dso_apk_entry (dso->real_name_hash);
				if (apk_entry != nullptr && apk_entry->fd != -1) {
					dso->handle = DsoLoader::load (apk_entry->fd, apk_entry->offset, dso_name, flags, dso->is_jni_library);
				}

				if (dso->handle != nullptr) {
					return dso->handle;
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

		static auto monodroid_dlopen (std::string_view const& name, int flags) noexcept -> void*
		{
			if (name.empty ()) [[unlikely]] {
				log_warn (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+"sv);
				return nullptr;
			}

			hash_t name_hash = crc32_hash (name);
			log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash for name '{}' is {:x}", name, name_hash);

			DSOCacheEntry *dso = find_dso_cache_entry (name_hash);
			return monodroid_dlopen (dso, name, flags);
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

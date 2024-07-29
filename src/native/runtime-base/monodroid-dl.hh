#pragma once

#include <dlfcn.h>
#include <android/dlext.h>

#include <mono/utils/mono-dl-fallback.h>
#include <java-interop-dlfcn.h>
#include <xxhash.hh>
#include <xamarin-app.hh>

#include "android-system.hh"
#include "monodroid-state.hh"
#include "search.hh"
#include "shared-constants.hh"
#include "startup-aware-lock.hh"
#include "util.hh"

namespace xamarin::android::internal
{
	class MonodroidDl
	{
		static inline xamarin::android::mutex   dso_handle_write_lock;

		static unsigned int convert_dl_flags (int flags) noexcept
		{
			unsigned int lflags = (flags & static_cast<int> (MONO_DL_LOCAL))
								  ? microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY
								  : microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_GLOBALLY;
			return lflags;
		}

		template<bool AotCache>
		[[gnu::always_inline, gnu::flatten]]
		static DSOCacheEntry* find_dso_cache_entry_common (hash_t hash) noexcept
		{
			DSOCacheEntry *arr;
			size_t arr_size;

			if constexpr (AotCache) {
				log_debug (LOG_ASSEMBLY, "Looking for hash 0x%x in AOT cache", hash);
				arr = aot_dso_cache;
				arr_size = application_config.number_of_aot_cache_entries;
			} else {
				log_debug (LOG_ASSEMBLY, "Looking for hash 0x%x in DSO cache", hash);
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
		static DSOCacheEntry* find_only_aot_cache_entry (hash_t hash) noexcept
		{
			constexpr bool IsAotCache = true;
			return find_dso_cache_entry_common<IsAotCache> (hash);
		}

		[[gnu::always_inline, gnu::flatten]]
		static DSOCacheEntry* find_only_dso_cache_entry (hash_t hash) noexcept
		{
			constexpr bool IsAotCache = false;
			return find_dso_cache_entry_common<IsAotCache> (hash);
		}

		[[gnu::always_inline, gnu::flatten]]
		static DSOCacheEntry* find_any_dso_cache_entry (hash_t hash) noexcept
		{
			// If we're asked to look in the AOT DSO cache, do it first.  This is because we're likely called from the
			// MonoVM's dlopen fallback handler and it will not be a request to resolved a p/invoke, but most likely to
			// find and load an AOT image for a managed assembly.  Since there might be naming/hash conflicts in this
			// scenario, we look at the AOT cache first.
			//
			// See: https://github.com/dotnet/android/issues/9081
			DSOCacheEntry *ret = find_only_aot_cache_entry (hash);
			if (ret != nullptr) {
				return ret;
			}

			return find_only_dso_cache_entry (hash);
		}

		static void* monodroid_dlopen_log_and_return (void *handle, char **err, const char *full_name, bool free_memory)
		{
			if (handle == nullptr && err != nullptr) {
				const char *load_error = dlerror ();
				if (load_error == nullptr) {
					load_error = "Unknown error";
				}
				*err = Util::monodroid_strdup_printf ("Could not load library '%s'. %s", full_name, load_error);
			}

			if (free_memory) {
				delete[] full_name;
			}

			return handle;
		}

		static void* monodroid_dlopen_ignore_component_or_load ([[maybe_unused]] hash_t name_hash, const char *name, int flags, char **err) noexcept
		{
			if (MonodroidState::is_startup_in_progress ()) {
				auto ignore_component = [&](const char *label, MonoComponent component) -> bool {
					if ((application_config.mono_components_mask & component) != component) {
						log_info (LOG_ASSEMBLY, "Mono '%s' component requested but not packaged, ignoring", label);
						return true;
					}

					return false;
				};

				switch (name_hash) {
					case SharedConstants::mono_component_debugger_hash:
						if (ignore_component ("Debugger", MonoComponent::Debugger)) {
							return nullptr;
						}
						break;

					case SharedConstants::mono_component_hot_reload_hash:
						if (ignore_component ("Hot Reload", MonoComponent::HotReload)) {
							return nullptr;
						}
						break;

					case SharedConstants::mono_component_diagnostics_tracing_hash:
						if (ignore_component ("Diagnostics Tracing", MonoComponent::Tracing)) {
							return nullptr;
						}
						break;
				}
			}

			unsigned int dl_flags = convert_dl_flags (flags);
			void * handle = AndroidSystem::load_dso_from_any_directories (name, dl_flags);
			if (handle != nullptr) {
				return monodroid_dlopen_log_and_return (handle, err, name, false /* name_needs_free */);
			}

			handle = AndroidSystem::load_dso (name, dl_flags, false /* skip_existing_check */);
			return monodroid_dlopen_log_and_return (handle, err, name, false /* name_needs_free */);
		}

	public:
		[[gnu::flatten]]
		static void* monodroid_dlopen (const char *name, int flags, char **err, bool use_aot_cache) noexcept
		{
			if (name == nullptr) {
				log_warn (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+");
				return nullptr;
			}

			hash_t name_hash = xxhash::hash (name, strlen (name));
			log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash for name '%s' is 0x%zx", name, name_hash);
			DSOCacheEntry *dso = use_aot_cache ? find_any_dso_cache_entry (name_hash) : find_only_dso_cache_entry (name_hash);
			log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash match %sfound, DSO name is '%s'", dso == nullptr ? "not " : "", dso == nullptr ? "<unknown>" : dso->name);

			if (dso == nullptr) {
				// DSO not known at build time, try to load it
				return monodroid_dlopen_ignore_component_or_load (name_hash, name, flags, err);
			} else if (dso->handle != nullptr) {
				return monodroid_dlopen_log_and_return (dso->handle, err, dso->name, false /* name_needs_free */);
			}

			if (dso->ignore) {
				log_info (LOG_ASSEMBLY, "Request to load '%s' ignored, it is known not to exist", dso->name);
				return nullptr;
			}

			StartupAwareLock lock (dso_handle_write_lock);
#if defined (RELEASE)
			if (AndroidSystem::is_embedded_dso_mode_enabled ()) {
				DSOApkEntry *apk_entry = dso_apk_entries;
				for (size_t i = 0; i < application_config.number_of_shared_libraries; i++) {
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
						return monodroid_dlopen_log_and_return (dso->handle, err, dso->name, false /* name_needs_free */);
					}
					break;
				}
			}
#endif
			unsigned int dl_flags = convert_dl_flags (flags);
			dso->handle = AndroidSystem::load_dso_from_any_directories (dso->name, dl_flags);

			if (dso->handle != nullptr) {
				return monodroid_dlopen_log_and_return (dso->handle, err, dso->name, false /* name_needs_free */);
			}

			dso->handle = AndroidSystem::load_dso_from_any_directories (name, dl_flags);
			return monodroid_dlopen_log_and_return (dso->handle, err, name, false /* name_needs_free */);
		}

		[[gnu::flatten]]
		static void* monodroid_dlopen (const char *name, int flags, char **err, [[maybe_unused]] void *user_data) noexcept
		{
			// We're called by MonoVM via a callback, we might need to return an AOT DSO.
			// See: https://github.com/dotnet/android/issues/9081
			constexpr bool USE_AOT_CACHE = true;
			return monodroid_dlopen (name, flags, err, USE_AOT_CACHE);
		}

		[[gnu::flatten]]
		static void* monodroid_dlsym (void *handle, const char *name, char **err, [[maybe_unused]] void *user_data)
		{
			void *s;
			char *e = nullptr;

			s = microsoft::java_interop::java_interop_lib_symbol (handle, name, &e);

			if (!s && err) {
				*err = Util::monodroid_strdup_printf ("Could not find symbol '%s': %s", name, e);
			}
			if (e) {
				java_interop_free (e);
			}

			return s;
		}
	};
}

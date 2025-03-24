#pragma once

#if !defined(PINVOKE_OVERRIDE_INLINE)
#warning The PINVOKE_OVERRIDE_INLINE macro must be defined before including this header file
#define PINVOKE_OVERRIDE_INLINE inline
#endif

#include <string>

#include "logger.hh"
#include "monodroid-dl.hh"
#include "pinvoke-override-api.hh"
#include "startup-aware-lock.hh"

namespace xamarin::android {
	PINVOKE_OVERRIDE_INLINE void*
	PinvokeOverride::load_library_symbol (const char *library_name, const char *symbol_name, void **dso_handle) noexcept
	{
		void *lib_handle = dso_handle == nullptr ? nullptr : *dso_handle;

		if (lib_handle == nullptr) {
			// We're being called as part of the p/invoke mechanism, we don't need to look in the AOT cache
			constexpr bool PREFER_AOT_CACHE = false;
			lib_handle = internal::MonodroidDl::monodroid_dlopen (library_name, MONO_DL_LOCAL, nullptr, PREFER_AOT_CACHE);
			if (lib_handle == nullptr) {
				log_warn (LOG_ASSEMBLY, "Shared library '{}' not loaded, p/invoke '{}' may fail", optional_string (library_name), optional_string (symbol_name));
				return nullptr;
			}

			if (dso_handle != nullptr) {
				void *expected_null = nullptr;
				if (!__atomic_compare_exchange (dso_handle, &expected_null, &lib_handle, false /* weak */, __ATOMIC_ACQUIRE /* success_memorder */, __ATOMIC_RELAXED /* xxxfailure_memorder */)) {
					log_debug (LOG_ASSEMBLY, "Library '{}' handle already cached by another thread", optional_string (library_name));
				}
			}
		}

		void *entry_handle = internal::MonodroidDl::monodroid_dlsym (lib_handle, symbol_name, nullptr, nullptr);
		if (entry_handle == nullptr) {
			log_warn (LOG_ASSEMBLY, "Symbol '{}' not found in shared library '{}', p/invoke may fail", optional_string (library_name), optional_string (symbol_name));
			return nullptr;
		}

		return entry_handle;
	}

	// `pinvoke_map_write_lock` MUST be held when calling this method
	PINVOKE_OVERRIDE_INLINE void*
	PinvokeOverride::load_library_entry (std::string const& library_name, std::string const& entrypoint_name, pinvoke_api_map_ptr api_map) noexcept
	{
		// Make sure some other thread hasn't just added the entry
		auto iter = api_map->find (entrypoint_name);
		if (iter != api_map->end () && iter->second != nullptr) {
			return iter->second;
		}

		void *entry_handle = load_library_symbol (library_name.c_str (), entrypoint_name.c_str ());
		if (entry_handle == nullptr) {
			// error already logged
			return nullptr;
		}

		log_debug (LOG_ASSEMBLY, "Caching p/invoke entry {} @ {}", library_name, entrypoint_name);
		(*api_map)[entrypoint_name] = entry_handle;
		return entry_handle;
	}

	PINVOKE_OVERRIDE_INLINE void
	PinvokeOverride::load_library_entry (const char *library_name, const char *entrypoint_name, PinvokeEntry &entry, void **dso_handle) noexcept
	{
		void *entry_handle = load_library_symbol (library_name, entrypoint_name, dso_handle);
		void *expected_null = nullptr;

		bool already_loaded = !__atomic_compare_exchange (
			/* ptr */              &entry.func,
			/* expected */         &expected_null,
			/* desired */          &entry_handle,
			/* weak */              false,
			/* success_memorder */  __ATOMIC_ACQUIRE,
			/* failure_memorder */  __ATOMIC_RELAXED
		);

		if (already_loaded) {
			log_debug (LOG_ASSEMBLY, "Entry '{}' from library '{}' already loaded by another thread", entrypoint_name, library_name);
		}
	}

	PINVOKE_OVERRIDE_INLINE void*
	PinvokeOverride::fetch_or_create_pinvoke_map_entry (std::string const& library_name, std::string const& entrypoint_name, hash_t entrypoint_name_hash, pinvoke_api_map_ptr api_map, bool need_lock) noexcept
	{
		auto iter = api_map->find (entrypoint_name, entrypoint_name_hash);
		if (iter != api_map->end () && iter->second != nullptr) {
			return iter->second;
		}

		if (!need_lock) {
			return load_library_entry (library_name, entrypoint_name, api_map);
		}

		internal::StartupAwareLock lock (pinvoke_map_write_lock);
		return load_library_entry (library_name, entrypoint_name, api_map);
	}

	PINVOKE_OVERRIDE_INLINE
	PinvokeEntry*
	PinvokeOverride::find_pinvoke_address (hash_t hash, const PinvokeEntry *entries, size_t entry_count) noexcept
	{
		while (entry_count > 0uz) {
			const size_t mid = entry_count / 2uz;
			const PinvokeEntry *const ret = entries + mid;
			const std::strong_ordering result = hash <=> ret->hash;

			if (result < 0) {
				entry_count = mid;
			} else if (result > 0) {
				entries = ret + 1;
				entry_count -= mid + 1uz;
			} else {
				return const_cast<PinvokeEntry*>(ret);
			}
		}

		return nullptr;
	}

	PINVOKE_OVERRIDE_INLINE void*
	PinvokeOverride::handle_other_pinvoke_request (const char *library_name, hash_t library_name_hash, const char *entrypoint_name, hash_t entrypoint_name_hash) noexcept
	{
		std::string lib_name {library_name};
		std::string entry_name {entrypoint_name};

		auto iter = other_pinvoke_map.find (lib_name, library_name_hash);
		void *handle = nullptr;
		if (iter == other_pinvoke_map.end ()) {
			internal::StartupAwareLock lock (pinvoke_map_write_lock);

			pinvoke_api_map_ptr lib_map;
			// Make sure some other thread hasn't just added the map
			iter = other_pinvoke_map.find (lib_name, library_name_hash);
			if (iter == other_pinvoke_map.end () || iter->second == nullptr) {
				lib_map = new pinvoke_api_map (1);
				other_pinvoke_map[lib_name] = lib_map;
			} else {
				lib_map = iter->second;
			}

			handle = fetch_or_create_pinvoke_map_entry (lib_name, entry_name, entrypoint_name_hash, lib_map, /* need_lock */ false);
		} else {
			if (iter->second == nullptr) [[unlikely]] {
				log_warn (LOG_ASSEMBLY, "Internal error: null entry in p/invoke map for key '{}'", optional_string (library_name));
				return nullptr; // fall back to `monodroid_dlopen`
			}

			handle = fetch_or_create_pinvoke_map_entry (lib_name, entry_name, entrypoint_name_hash, iter->second, /* need_lock */ true);
		}

		return handle;
	}
}

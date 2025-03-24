#pragma once

#include <mutex>
#include <string>

// NDEBUG causes robin_map.h not to include <iostream> which, in turn, prevents indirect inclusion of <mutex>. <mutex>
// conflicts with our std::mutex definition in cppcompat.hh
#if !defined (NDEBUG)
#define NDEBUG
#define NDEBUG_UNDEFINE
#endif

// hush some compiler warnings
#if defined (__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunused-parameter"
#endif // __clang__

#include <tsl/robin_map.h>

#if defined (__clang__)
#pragma clang diagnostic pop
#endif // __clang__

#if defined (NDEBUG_UNDEFINE)
#undef NDEBUG
#undef NDEBUG_UNDEFINE
#endif

#include "../runtime-base/monodroid-dl.hh"
#include <shared/xxhash.hh>

namespace xamarin::android {
	struct PinvokeEntry
	{
		hash_t      hash;
		const char *name;
		void       *func;
	};

	struct string_hash
	{
		[[gnu::always_inline]]
		xamarin::android::hash_t operator() (std::string const& s) const noexcept
		{
			return xamarin::android::xxhash::hash (s.c_str (), s.length ());
		}
	};

	class PinvokeOverride
	{
		using pinvoke_api_map = tsl::robin_map<
			std::string,
			void*,
			string_hash,
			std::equal_to<std::string>,
			std::allocator<std::pair<std::string, void*>>,
			true
		>;

		using pinvoke_api_map_ptr = pinvoke_api_map*;
		using pinvoke_library_map = tsl::robin_map<
			std::string,
			pinvoke_api_map_ptr,
			string_hash,
			std::equal_to<std::string>,
			std::allocator<std::pair<std::string, pinvoke_api_map_ptr>>,
			true
		>;

		static inline constexpr pinvoke_library_map::size_type LIBRARY_MAP_INITIAL_BUCKET_COUNT = 1uz;

	public:
		[[gnu::always_inline]]
		static auto load_library_symbol (const char *library_name, const char *symbol_name, void **dso_handle = nullptr) noexcept -> void*
		{
			void *lib_handle = dso_handle == nullptr ? nullptr : *dso_handle;

			if (lib_handle == nullptr) {
				// We're being called as part of the p/invoke mechanism, we don't need to look in the AOT cache
				constexpr bool PREFER_AOT_CACHE = false;
				lib_handle = MonodroidDl::monodroid_dlopen (library_name, microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY, PREFER_AOT_CACHE);
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

			void *entry_handle = MonodroidDl::monodroid_dlsym (lib_handle, symbol_name);
			if (entry_handle == nullptr) {
				log_warn (LOG_ASSEMBLY, "Symbol '{}' not found in shared library '{}', p/invoke may fail", optional_string (library_name), optional_string (symbol_name));
				return nullptr;
			}

			return entry_handle;
		}

		static auto load_library_entry (std::string const& library_name, std::string const& entrypoint_name, pinvoke_api_map_ptr api_map) noexcept -> void*
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

		static void load_library_entry (const char *library_name, const char *entrypoint_name, PinvokeEntry &entry, void **dso_handle) noexcept
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

		static auto fetch_or_create_pinvoke_map_entry (std::string const& library_name, std::string const& entrypoint_name, hash_t entrypoint_name_hash, pinvoke_api_map_ptr api_map, bool need_lock) noexcept -> void*
		{
			auto iter = api_map->find (entrypoint_name, entrypoint_name_hash);
			if (iter != api_map->end () && iter->second != nullptr) {
				return iter->second;
			}

			if (!need_lock) {
				return load_library_entry (library_name, entrypoint_name, api_map);
			}

			StartupAwareLock lock (pinvoke_map_write_lock);
			return load_library_entry (library_name, entrypoint_name, api_map);
		}

		[[gnu::always_inline]]
		static auto find_pinvoke_address (hash_t hash, const PinvokeEntry *entries, size_t entry_count) noexcept -> PinvokeEntry*
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

		[[gnu::always_inline, gnu::flatten]]
		static auto handle_other_pinvoke_request (const char *library_name, hash_t library_name_hash, const char *entrypoint_name, hash_t entrypoint_name_hash) noexcept -> void*
		{
			std::string lib_name {library_name};
			std::string entry_name {entrypoint_name};

			auto iter = other_pinvoke_map.find (lib_name, library_name_hash);
			void *handle = nullptr;
			if (iter == other_pinvoke_map.end ()) {
				StartupAwareLock lock (pinvoke_map_write_lock);

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

		static auto monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name) noexcept -> void*;

	private:
		static inline std::mutex          pinvoke_map_write_lock{};
		static inline pinvoke_library_map other_pinvoke_map{};
		static inline void *system_native_library_handle = nullptr;
		static inline void *system_security_cryptography_native_android_library_handle = nullptr;
		static inline void *system_io_compression_native_library_handle = nullptr;
		static inline void *system_globalization_native_library_handle = nullptr;
	};
}

#pragma once

#include <string>

#include "cppcompat.hh"
#include "robin_map.hh"
#include "xxhash.hh"

namespace xamarin::android {
	struct PinvokeEntry
	{
		hash_t      hash;
		const char *name;
		void       *func;
	};

	struct string_hash
	{
		force_inline xamarin::android::hash_t operator() (std::string const& s) const noexcept
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
		static void* load_library_symbol (const char *library_name, const char *symbol_name, void **dso_handle = nullptr) noexcept;
		static void* load_library_entry (std::string const& library_name, std::string const& entrypoint_name, pinvoke_api_map_ptr api_map) noexcept;
		static void  load_library_entry (const char *library_name, const char *entrypoint_name, PinvokeEntry &entry, void **dso_handle) noexcept;
		static void* fetch_or_create_pinvoke_map_entry (std::string const& library_name, std::string const& entrypoint_name, hash_t entrypoint_name_hash, pinvoke_api_map_ptr api_map, bool need_lock) noexcept;
		static PinvokeEntry* find_pinvoke_address (hash_t hash, const PinvokeEntry *entries, size_t entry_count) noexcept;
		static void* handle_other_pinvoke_request (const char *library_name, hash_t library_name_hash, const char *entrypoint_name, hash_t entrypoint_name_hash) noexcept;
		static void* monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name);

	private:
		static xamarin::android::mutex  pinvoke_map_write_lock;
		static pinvoke_library_map    other_pinvoke_map;

#if defined(PRECOMPILED)
		static inline void *system_native_library_handle = nullptr;
		static inline void *system_security_cryptography_native_android_library_handle = nullptr;
		static inline void *system_io_compression_native_library_handle = nullptr;
		static inline void *system_globalization_native_library_handle = nullptr;
#endif
	};
}

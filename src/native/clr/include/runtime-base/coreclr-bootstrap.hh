#pragma once

#include <cstddef>
#include <cstdint>

#include <xamarin-app.hh>

namespace xamarin::android
{
	class CoreClrBootstrap final
	{
		static inline const uint8_t *body = nullptr;
		static inline uint32_t body_size = 0;
		static inline uint32_t strings_start = 0;
		static inline uint32_t env_offset = 0;
		static inline uint32_t sys_offset = 0;
		static inline uint32_t preload_offset = 0;

	public:
		static inline ApplicationConfig config {};
		static inline DSOCacheEntry *dso_cache = nullptr;
		static inline const char **property_names = nullptr;
		static inline char **property_values = nullptr;
		static inline uint32_t preload_count = 0;
		static inline uint32_t preload_stride = 0;

		static void initialize () noexcept;
		static void setup_environment () noexcept;
		static auto string (uint32_t offset, bool required = true) noexcept -> const char*;
		static auto pair (bool system, uint32_t index) noexcept -> AppEnvironmentVariable;
		static auto preload_index (uint32_t index) noexcept -> uint32_t;
		static auto lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*;

	private:
		static auto read (uint64_t offset) noexcept -> uint32_t;
	};
}

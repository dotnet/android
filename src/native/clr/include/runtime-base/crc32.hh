#pragma once

#include <cstddef>
#include <cstdint>
#include <limits>
#include <string_view>

extern "C" uint32_t CompressionNative_Crc32 (uint32_t crc, uint8_t *buffer, int32_t len);

namespace xamarin::android {
	[[gnu::always_inline]]
	inline auto crc32_hash (const char *value, size_t len) noexcept -> uint32_t
	{
		if (len == 0) [[unlikely]] {
			return std::numeric_limits<uint32_t>::max ();
		}

		return CompressionNative_Crc32 (0, reinterpret_cast<uint8_t*>(const_cast<char*>(value)), static_cast<int32_t>(len));
	}

	[[gnu::always_inline]]
	inline auto crc32_hash (std::string_view const& value) noexcept -> uint32_t
	{
		return crc32_hash (value.data (), value.length ());
	}
}

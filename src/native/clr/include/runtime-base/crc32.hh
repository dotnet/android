#pragma once

#include <cstddef>
#include <cstdint>
#include <limits>
#include <string_view>

namespace xamarin::android {
	using hash_t = uint32_t;
	static constexpr hash_t CRC32_POLYNOMIAL = 0xedb88320;

	[[gnu::always_inline]]
	constexpr auto crc32_hash (const char *value, size_t len) noexcept -> hash_t
	{
		if (len == 0) [[unlikely]] {
			return std::numeric_limits<uint32_t>::max ();
		}

		hash_t crc = 0xffffffff;
		for (size_t i = 0; i < len; i++) {
			crc ^= static_cast<uint8_t>(value [i]);
			for (size_t bit = 0; bit < 8; bit++) {
				crc = (crc >> 1) ^ ((crc & 1) != 0 ? CRC32_POLYNOMIAL : 0);
			}
		}

		return ~crc;
	}

	template<size_t Size>
	consteval auto crc32_hash (const char (&value)[Size]) noexcept -> hash_t
	{
		return crc32_hash (value, Size - 1);
	}

	[[gnu::always_inline]]
	constexpr auto crc32_hash (std::string_view const& value) noexcept -> hash_t
	{
		return crc32_hash (value.data (), value.length ());
	}
}

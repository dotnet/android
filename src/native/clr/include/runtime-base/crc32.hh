#pragma once

#include <cstddef>
#include <cstdint>
#include <limits>
#include <string_view>

extern "C" uint32_t CompressionNative_Crc32 (uint32_t crc, uint8_t *buffer, int32_t len) noexcept;

namespace xamarin::android {
	using hash_t = uint32_t;

	[[gnu::always_inline]]
	inline auto crc32_hash (const char *value, size_t len) noexcept -> hash_t
	{
		// Preserve the empty-input sentinel rather than returning the standard empty CRC32 (0).
		if (len == 0) [[unlikely]] {
			return std::numeric_limits<uint32_t>::max ();
		}

		// The native compression ABI takes a mutable pointer but does not modify the bytes.
		auto bytes = const_cast<uint8_t*>(reinterpret_cast<const uint8_t*>(value));
		constexpr int32_t MaxChunkSize = std::numeric_limits<int32_t>::max ();
		// IEEE CRC32 (0xedb88320); the compression shim applies the initial/final complements.
		hash_t crc = 0;
		while (len > MaxChunkSize) [[unlikely]] {
			crc = CompressionNative_Crc32 (crc, bytes, MaxChunkSize);
			bytes += MaxChunkSize;
			len -= MaxChunkSize;
		}

		return CompressionNative_Crc32 (crc, bytes, static_cast<int32_t>(len));
	}

	[[gnu::always_inline]]
	inline auto crc32_hash (std::string_view const& value) noexcept -> hash_t
	{
		return crc32_hash (value.data (), value.length ());
	}
}

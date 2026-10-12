#pragma once

#include <cstddef>
#include <cstdint>

namespace xamarin::android {
	struct BinaryBlobPayload {
		const uint8_t *data;
		uint32_t size;
	};

	class BinaryBlobLoader {
	public:
		// The returned bytes and the containing DSO remain valid for the process lifetime.
		static auto load (const char *symbol) noexcept -> BinaryBlobPayload;
		// Missing optional libraries or symbols return an empty payload.
		static auto load_optional (const char *symbol) noexcept -> BinaryBlobPayload;
		// CoreCLR may opt into compression; decoded bodies remain valid for the process lifetime.
		static auto decode (const uint8_t *blob, size_t size, const char *symbol) noexcept -> BinaryBlobPayload;
		// NativeAOT only accepts mapped raw bodies, never allocating a decompression buffer.
		static auto read_uncompressed (const uint8_t *blob, size_t size, const char *symbol) noexcept -> BinaryBlobPayload;
	};
}

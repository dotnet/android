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
		// Decode linked read-only data using the same envelope and process-lifetime ownership.
		static auto decode (const uint8_t *blob, size_t size, const char *symbol) noexcept -> BinaryBlobPayload;
	};
}

#pragma once

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
	};
}

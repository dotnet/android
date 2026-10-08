#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <dlfcn.h>
#include <pthread.h>
#include <source_location>

#include <runtime-base/binary-blob-loader.hh>
#include <runtime-base/zstd.hh>
#include <shared/helpers.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

namespace {
	constexpr uint32_t blob_magic = 0x42424c42; // BLBB
	constexpr uint32_t maximum_raw_size = 256 * 1024 * 1024;

	struct BlobHeader {
		uint32_t magic;
		uint16_t version;
		uint16_t flags;
		uint32_t stored;
		uint32_t raw;

		// BLBB v1 flags: 0 = uncompressed mapped body, 1 = Zstd-compressed body.
		[[nodiscard]] auto is_compressed () const noexcept -> bool
		{
			return flags == 1;
		}

		[[nodiscard]] auto is_valid () const noexcept -> bool
		{
			return magic == blob_magic && version == 1 && flags <= 1 && stored != 0 &&
				raw != 0 && raw <= maximum_raw_size && (is_compressed () || stored == raw);
		}
	};
	static_assert (sizeof (BlobHeader) == 16);
	constexpr size_t envelope_size = sizeof (BlobHeader);

	pthread_once_t library_once = PTHREAD_ONCE_INIT;
	void *library = nullptr;

	void open_library () noexcept
	{
		library = ::dlopen ("libbinary_blobs.so", RTLD_NOW | RTLD_LOCAL);
		if (library == nullptr) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot load binary blobs: %s", ::dlerror ());
		}
	}

	// Used only for payloads whose producer opted into Zstd compression.
	auto decode_compressed_payload (const uint8_t *blob, const BlobHeader &header, const char *symbol) noexcept -> BinaryBlobPayload
	{
		void *decoded = std::malloc (header.raw);
		if (decoded == nullptr) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot allocate %u bytes for binary blob '%s'", header.raw, symbol);
		}
		size_t decompressed = ZSTD_decompress (decoded, header.raw, blob + envelope_size, header.stored);
		if (ZSTD_isError (decompressed) || decompressed != header.raw) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot decode binary blob '%s': %s (expected %u bytes, got %zu)",
				symbol, ZSTD_isError (decompressed) ? ZSTD_getErrorName (decompressed) : "wrong uncompressed size", header.raw, decompressed);
		}
		// The lookup returns pointers into the decoded body, so it is retained for the process lifetime.
		return { static_cast<const uint8_t*> (decoded), header.raw };
	}
}

auto BinaryBlobLoader::load (const char *symbol) noexcept -> BinaryBlobPayload
{
	int result = ::pthread_once (&library_once, open_library);
	if (result != 0) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Cannot initialize binary blob loader: %d", result);
	}

	const uint8_t *blob = static_cast<const uint8_t*> (::dlsym (library, symbol));
	if (blob == nullptr) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Missing binary blob symbol '%s'", symbol);
	}

	BlobHeader header;
	std::memcpy (&header, blob, sizeof (header));
	if (!header.is_valid ()) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Invalid binary blob envelope for '%s'", symbol);
	}

	if (!header.is_compressed ()) {
		return { blob + envelope_size, header.raw };
	}

	return decode_compressed_payload (blob, header, symbol);
}

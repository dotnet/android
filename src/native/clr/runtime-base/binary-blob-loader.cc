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
#include <shared/log_functions.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

namespace {
	constexpr uint32_t blob_magic = 0x42424c42; // BLBB
	constexpr uint32_t maximum_raw_size = 256 * 1024 * 1024;

	// A view of BLBB's little-endian bytes, not a typed object cast or header copy.
	class BlobHeader {
		const uint8_t *data;

	public:
		static constexpr size_t Size = 16;
		explicit BlobHeader (const uint8_t *data) noexcept : data { data } {}
		[[nodiscard]] auto stored () const noexcept -> uint32_t { return read_uint32 (8); }
		[[nodiscard]] auto raw () const noexcept -> uint32_t { return read_uint32 (12); }
		// BLBB v1 flags: 0 = uncompressed mapped body, 1 = Zstd-compressed body.
		[[nodiscard]] auto is_compressed () const noexcept -> bool
		{
			return read_uint16 (6) == 1;
		}

		[[nodiscard]] auto is_valid () const noexcept -> bool
		{
			return read_uint32 (0) == blob_magic && read_uint16 (4) == 1 && read_uint16 (6) <= 1 && stored () != 0 &&
				raw () != 0 && raw () <= maximum_raw_size && (is_compressed () || stored () == raw ());
		}

	private:
		auto read_uint16 (size_t offset) const noexcept -> uint16_t
		{
			return static_cast<uint16_t>(data[offset]) | static_cast<uint16_t>(data[offset + 1]) << 8;
		}

		auto read_uint32 (size_t offset) const noexcept -> uint32_t
		{
			return static_cast<uint32_t>(data[offset]) | static_cast<uint32_t>(data[offset + 1]) << 8 |
				static_cast<uint32_t>(data[offset + 2]) << 16 | static_cast<uint32_t>(data[offset + 3]) << 24;
		}
	};
	constexpr size_t envelope_size = BlobHeader::Size;

	auto read_header (const uint8_t *blob, size_t size, const char *symbol) noexcept -> BlobHeader
	{
		if (blob == nullptr || size < envelope_size) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Missing or truncated binary blob '%s'", symbol);
		}
		BlobHeader header { blob };
		if (!header.is_valid () || static_cast<uint64_t>(header.stored ()) + envelope_size != size) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Invalid binary blob envelope for '%s'", symbol);
		}
		return header;
	}

	pthread_once_t library_once = PTHREAD_ONCE_INIT;
	void *library = nullptr;

	void open_library () noexcept
	{
		library = ::dlopen ("libbinary_blobs.so", RTLD_NOW | RTLD_LOCAL);
		if (library == nullptr) {
			const char *error = ::dlerror ();
			// Android's loader exposes missing-file status through dlerror, not errno.
			if (error != nullptr && std::strstr (error, "library \"libbinary_blobs.so\" not found") != nullptr) {
				log_debugf (LOG_ASSEMBLY, "Optional binary blob library is absent: %s", error);
				return;
			}
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot load binary blobs: %s", optional_string (error));
		}
	}

	// Used only for payloads whose producer opted into Zstd compression.
	auto decode_compressed_payload (const uint8_t *blob, const BlobHeader &header, const char *symbol) noexcept -> BinaryBlobPayload
	{
		void *decoded = std::malloc (header.raw ());
		if (decoded == nullptr) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot allocate %u bytes for binary blob '%s'", header.raw (), symbol);
		}
		size_t decompressed = ZSTD_decompress (decoded, header.raw (), blob + envelope_size, header.stored ());
		if (ZSTD_isError (decompressed) || decompressed != header.raw ()) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot decode binary blob '%s': %s (expected %u bytes, got %zu)",
				symbol, ZSTD_isError (decompressed) ? ZSTD_getErrorName (decompressed) : "wrong uncompressed size", header.raw (), decompressed);
		}
		// The lookup returns pointers into the decoded body, so it is retained for the process lifetime.
		return { static_cast<const uint8_t*> (decoded), header.raw () };
	}
	auto load_payload (const char *symbol, bool optional) noexcept -> BinaryBlobPayload
	{
		int result = ::pthread_once (&library_once, open_library);
		if (result != 0) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot initialize binary blob loader: %d", result);
		}
		if (library == nullptr) {
			if (optional) {
				return {};
			}
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Cannot load required binary blob '%s': libbinary_blobs.so was not found", symbol);
		}

		const uint8_t *blob = static_cast<const uint8_t*> (::dlsym (library, symbol));
		if (blob == nullptr) {
			if (optional) {
				log_debugf (LOG_ASSEMBLY, "Optional binary blob symbol '%s' is absent", symbol);
				return {};
			}
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Missing binary blob symbol '%s'", symbol);
		}

		BlobHeader header { blob };
		if (!header.is_valid ()) [[unlikely]] {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Invalid binary blob envelope for '%s'", symbol);
		}

		return BinaryBlobLoader::decode (blob, envelope_size + static_cast<size_t>(header.stored ()), symbol);
	}
}

auto BinaryBlobLoader::decode (const uint8_t *blob, size_t size, const char *symbol) noexcept -> BinaryBlobPayload
{
	BlobHeader header = read_header (blob, size, symbol);
	if (!header.is_compressed ()) {
		return { blob + envelope_size, header.raw () };
	}
	return decode_compressed_payload (blob, header, symbol);
}

auto BinaryBlobLoader::read_uncompressed (const uint8_t *blob, size_t size, const char *symbol) noexcept -> BinaryBlobPayload
{
	BlobHeader header = read_header (blob, size, symbol);
	if (header.is_compressed ()) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Compressed NativeAOT binary blob '%s' is not supported", symbol);
	}
	return { blob + envelope_size, header.raw () };
}

auto BinaryBlobLoader::load (const char *symbol) noexcept -> BinaryBlobPayload
{
	return load_payload (symbol, false);
}

auto BinaryBlobLoader::load_optional (const char *symbol) noexcept -> BinaryBlobPayload
{
	return load_payload (symbol, true);
}

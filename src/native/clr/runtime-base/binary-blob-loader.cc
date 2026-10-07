#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <dlfcn.h>
#include <link.h>
#include <limits>
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
	};
	static_assert (sizeof (BlobHeader) == 16);
	constexpr size_t envelope_size = sizeof (BlobHeader);

	struct SymbolExtent {
		const uint8_t *symbol;
		size_t length;
	};

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

	auto find_read_only_extent (dl_phdr_info *info, size_t, void *context) noexcept -> int
	{
		auto& query = *static_cast<SymbolExtent*> (context);
		uintptr_t symbol = reinterpret_cast<uintptr_t> (query.symbol);
		for (ElfW(Half) i = 0; i < info->dlpi_phnum; i++) {
			const ElfW(Phdr)& phdr = info->dlpi_phdr [i];
			if (phdr.p_type != PT_LOAD || phdr.p_vaddr > std::numeric_limits<uintptr_t>::max () - info->dlpi_addr) {
				continue;
			}
			uintptr_t start = info->dlpi_addr + phdr.p_vaddr;
			if (symbol < start || symbol - start >= phdr.p_memsz) {
				continue;
			}
			uintptr_t offset = symbol - start;
			if (phdr.p_flags == PF_R && phdr.p_filesz <= phdr.p_memsz && offset < phdr.p_filesz) {
				query.length = phdr.p_filesz - offset;
			}
			return 1;
		}
		return 0;
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

	SymbolExtent extent { blob, 0 };
	if (::dl_iterate_phdr (find_read_only_extent, &extent) != 1 || extent.length < envelope_size) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Binary blob '%s' is not in a file-backed, read-only ELF load segment", symbol);
	}

	BlobHeader header;
	std::memcpy (&header, blob, sizeof (header));
	if (header.magic != blob_magic || header.version != 1 || header.flags > 1 || header.stored == 0 ||
	    header.raw == 0 || header.raw > maximum_raw_size || header.stored > extent.length - envelope_size ||
	    (header.flags == 0 && header.stored != header.raw)) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Invalid binary blob envelope or ELF extent for '%s'", symbol);
	}

	if (header.flags == 0) {
		return { blob + envelope_size, header.raw };
	}

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
	// The lookup returns pointers into the body, so neither the DSO nor a decoded buffer is released.
	return { static_cast<const uint8_t*> (decoded), header.raw };
}

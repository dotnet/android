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
	constexpr size_t envelope_size = 16;

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

	template<typename T>
	auto read_unaligned (const uint8_t *data) noexcept -> T
	{
		T value;
		std::memcpy (&value, data, sizeof (value));
		return value;
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

	uint32_t magic = read_unaligned<uint32_t> (blob);
	uint16_t version = read_unaligned<uint16_t> (blob + 4);
	uint16_t flags = read_unaligned<uint16_t> (blob + 6);
	uint32_t stored = read_unaligned<uint32_t> (blob + 8);
	uint32_t raw = read_unaligned<uint32_t> (blob + 12);
	if (magic != blob_magic || version != 1 || flags > 1 || stored == 0 ||
	    raw == 0 || raw > maximum_raw_size || stored > extent.length - envelope_size ||
	    (flags == 0 && stored != raw)) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Invalid binary blob envelope or ELF extent for '%s'", symbol);
	}

	if (flags == 0) {
		return { blob + envelope_size, raw };
	}

	void *decoded = std::malloc (raw);
	if (decoded == nullptr) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Cannot allocate %u bytes for binary blob '%s'", raw, symbol);
	}
	size_t decompressed = ZSTD_decompress (decoded, raw, blob + envelope_size, stored);
	if (ZSTD_isError (decompressed) || decompressed != raw) [[unlikely]] {
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
			"Cannot decode binary blob '%s': %s (expected %u bytes, got %zu)",
			symbol, ZSTD_isError (decompressed) ? ZSTD_getErrorName (decompressed) : "wrong uncompressed size", raw, decompressed);
	}
	// The lookup returns pointers into the body, so neither the DSO nor a decoded buffer is released.
	return { static_cast<const uint8_t*> (decoded), raw };
}

// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <cstddef>

//
// Minimal declarations for the Zstandard decompression functions that are exported by
// `libSystem.IO.Compression.Native`, which ships in the .NET runtime pack and is linked
// into our runtime. The assembly store compresses assemblies with Zstd at build time and
// we decompress them here at load time.
//
// We declare only the few entry points we need instead of pulling in `zstd.h`.
//
extern "C" {
	size_t ZSTD_decompress (void *dst, size_t dst_capacity, const void *src, size_t compressed_size) noexcept;
	unsigned ZSTD_isError (size_t code) noexcept;
	const char* ZSTD_getErrorName (size_t code) noexcept;
}

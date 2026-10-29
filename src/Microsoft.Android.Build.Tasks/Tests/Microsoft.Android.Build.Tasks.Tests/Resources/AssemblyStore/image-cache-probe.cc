#include <algorithm>
#include <array>
#include <atomic>
#include <barrier>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <thread>
#include <vector>

#include <host/assembly-store.hh>
#include <runtime-base/monodroid-state.hh>
#include <runtime-base/zstd.hh>
#include <shared/helpers.hh>
#include <shared/log_functions.hh>

using namespace xamarin::android;

extern "C" {
	void *ZSTD_createCCtx ();
	size_t ZSTD_compressBound (size_t);
	size_t ZSTD_compress2 (void*, void*, size_t, const void*, size_t);
	size_t ZSTD_freeCCtx (void*);

	// Published images intentionally live until process exit.
	const char *__asan_default_options () { return "detect_leaks=0"; }
}

namespace {
	std::atomic<unsigned> copies {0};
	std::atomic<unsigned> decompressions {0};

	void require (bool condition, const char *message)
	{
		if (!condition) {
			Helpers::abort_application (message);
		}
	}

	template<typename T>
	void append (std::vector<uint8_t> &bytes, const T &value)
	{
		auto start = reinterpret_cast<const uint8_t*>(&value);
		bytes.insert (bytes.end (), start, start + sizeof (value));
	}

	auto make_store (bool compressed) -> std::vector<uint8_t>
	{
		const char *names[] = {"First.dll", "Second.dll"};
		std::array<AssemblyStoreIndexEntry, 4> index {{
			{crc32_hash ("First"), 0, 0}, {crc32_hash ("First.dll"), 0, 0},
			{crc32_hash ("Second"), 1, 0}, {crc32_hash ("Second.dll"), 1, 0},
		}};
		std::sort (index.begin (), index.end (), [] (const auto &a, const auto &b) { return a.name_hash < b.name_hash; });
		std::vector<uint8_t> bytes;
		append (bytes, AssemblyStoreHeader {ASSEMBLY_STORE_MAGIC, ASSEMBLY_STORE_FORMAT_VERSION, 2, 4, sizeof (index)});
		for (const auto &entry : index) {
			append (bytes, entry);
		}
		size_t descriptors_offset = bytes.size ();
		bytes.resize (bytes.size () + 2 * sizeof (AssemblyStoreEntryDescriptor));
		for (const char *name : names) {
			append (bytes, static_cast<uint32_t>(std::strlen (name)));
			bytes.insert (bytes.end (), name, name + std::strlen (name));
		}
		for (uint32_t i = 0; i < 2; ++i) {
			std::vector<uint8_t> image (i == 0 ? 21800 : 16168, static_cast<uint8_t>(i + 1));
			uint32_t offset = static_cast<uint32_t>(bytes.size ());
			if (compressed && i == 1) {
				append (bytes, CompressedAssemblyHeader {COMPRESSED_DATA_MAGIC, 7, static_cast<uint32_t>(image.size ())});
				std::vector<uint8_t> encoded (ZSTD_compressBound (image.size ()));
				void *context = ZSTD_createCCtx ();
				require (context != nullptr, "Unable to create Zstd compressor");
				size_t size = ZSTD_compress2 (context, encoded.data (), encoded.size (), image.data (), image.size ());
				require (!ZSTD_isError (size), "Unable to compress test image");
				require (!ZSTD_isError (ZSTD_freeCCtx (context)), "Unable to release Zstd compressor");
				bytes.insert (bytes.end (), encoded.begin (), encoded.begin () + size);
			} else {
				bytes.insert (bytes.end (), image.begin (), image.end ());
			}
			// Reverse the mapping so descriptor and runtime indices cannot be confused.
			AssemblyStoreEntryDescriptor descriptor {1 - i, offset, static_cast<uint32_t>(bytes.size () - offset), 0, 0, 0, 0};
			std::memcpy (bytes.data () + descriptors_offset + i * sizeof (descriptor), &descriptor, sizeof (descriptor));
		}
		return bytes;
	}

	auto open (const char *name, int64_t expected_size) -> uint8_t*
	{
		int64_t size = -1;
		auto data = static_cast<uint8_t*>(AssemblyStore::open_assembly (name, size));
		require (data != nullptr && size == expected_size, "Incorrect assembly image or size");
		return data;
	}

	auto first_access (bool concurrent) -> std::array<uint8_t*, 2>
	{
		constexpr size_t thread_count = 16;
		std::array<std::array<uint8_t*, 2>, thread_count> results {};
		std::barrier start (static_cast<ptrdiff_t>(thread_count));
		auto probe = [&] (size_t i) {
			if (concurrent) {
				start.arrive_and_wait ();
			}
			results[i][0] = open (i % 2 == 0 ? "First" : "First.dll", 21800);
			results[i][1] = open (i % 2 == 0 ? "Second.dll" : "Second", 16168);
			for (size_t image = 0; image < 2; ++image) {
				size_t size = image == 0 ? 21800 : 16168;
				require (std::all_of (results[i][image], results[i][image] + size,
					[image] (uint8_t value) { return value == image + 1; }), "Image published before initialization");
			}
		};
		if (concurrent) {
			std::vector<std::thread> threads;
			for (size_t i = 0; i < thread_count; ++i) {
				threads.emplace_back (probe, i);
			}
			for (auto &thread : threads) {
				thread.join ();
			}
		} else {
			for (size_t i = 0; i < thread_count; ++i) {
				probe (i);
			}
		}
		for (const auto &result : results) {
			require (result == results[0], "Probes or aliases allocated different image buffers");
		}
		require (results[0][0] != results[0][1], "Different physical entries shared a buffer");
		return results[0];
	}
}

void xamarin::android::log_debugf (LogCategories, const char *format, ...) noexcept
{
	if (std::strcmp (format, "Copying assembly data to an r/w memory area") == 0) {
		++copies;
	} else if (std::strncmp (format, "Decompressing assembly ", 22) == 0) {
		++decompressions;
	}
}

void Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	std::fprintf (stderr, "%s\n", message);
	std::exit (EXIT_FAILURE);
}

void Helpers::abort_applicationf (LogCategories, std::source_location, const char *format, ...) noexcept
{
	va_list args;
	va_start (args, format);
	std::vfprintf (stderr, format, args);
	va_end (args);
	std::fputc ('\n', stderr);
	std::exit (EXIT_FAILURE);
}

int main (int argc, char **argv)
{
	require (argc == 2, "Expected startup or concurrent mode");
	bool concurrent = std::strcmp (argv[1], "concurrent") == 0;
	require (concurrent || std::strcmp (argv[1], "startup") == 0, "Invalid probe mode");
	if (concurrent) {
		MonodroidState::mark_startup_done ();
	}
	std::vector<std::array<uint8_t*, 2>> retained;
	for (unsigned configuration = 0; configuration < 32; ++configuration) {
		bool compressed = false;
#if defined (RELEASE)
		compressed = configuration % 2 != 0;
#endif
		auto store = make_store (compressed);
		unsigned previous_copies = copies;
		unsigned previous_decompressions = decompressions;
		AssemblyStore::configure_from_payload (store.data (), "image-cache-test");
		auto images = first_access (concurrent);
		require (copies - previous_copies == (compressed ? 1u : 2u), "Expected exactly one copy per raw entry");
		require (decompressions - previous_decompressions == (compressed ? 1u : 0u), "Expected exactly one decompression per compressed entry");
		images[0][0] = 42;
		images[1][16167] = 43;
		for (unsigned probe = 0; probe < 2000; ++probe) {
			require (open ("First.dll", 21800) == images[0] && images[0][0] == 42, "Repeated probe lost writable image");
			require (open ("Second", 16168) == images[1] && images[1][16167] == 43, "Repeated probe lost second image");
		}
		require (copies - previous_copies == (compressed ? 1u : 2u), "Repeated probes copied images again");
		require (decompressions - previous_decompressions == (compressed ? 1u : 0u), "Repeated probes decompressed images again");
		retained.push_back (images);
		// Poison the payload: all returned images must be independent writable buffers.
		std::fill (store.begin (), store.end (), 0);
		for (const auto &old : retained) {
			require (old[0][0] == 42 && old[0][21799] == 1 && old[1][0] == 2 && old[1][16167] == 43,
				"Reconfiguration invalidated a previously returned image");
		}
	}
	std::puts ("Image cache checks passed");
	return EXIT_SUCCESS;
}

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <thread>
#include <vector>

#include <host/assembly-store.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

static std::atomic<int> decompressions {0};
static std::atomic<int> allocations {0};
static std::mutex gate_mutex;
static std::condition_variable gate;

static void wait_for_second_call (std::atomic<int> &calls)
{
	if (calls.fetch_add (1) == 0) {
		// Give an unsynchronized second probe time to enter before the first publishes its result.
		std::unique_lock lock (gate_mutex);
		gate.wait_for (lock, std::chrono::milliseconds (500), [&] { return calls.load () > 1; });
	} else {
		gate.notify_one ();
	}
}

extern "C" uint32_t CompressionNative_Crc32 (uint32_t, uint8_t*, int32_t) noexcept
{
	return 42;
}

extern "C" void* assembly_store_probe_malloc (size_t size) noexcept
{
	wait_for_second_call (allocations);
	return std::malloc (size);
}

extern "C" size_t ZSTD_decompress (void *destination, size_t capacity, const void*, size_t) noexcept
{
	wait_for_second_call (decompressions);
	std::memset (destination, 0x5a, capacity);
	return capacity;
}

extern "C" unsigned ZSTD_isError (size_t) noexcept { return 0; }
extern "C" const char* ZSTD_getErrorName (size_t) noexcept { return ""; }

void Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	std::fprintf (stderr, "%s\n", message);
	std::abort ();
}

void Helpers::abort_applicationf (LogCategories, std::source_location, const char *format, ...) noexcept
{
	va_list args;
	va_start (args, format);
	std::vfprintf (stderr, format, args);
	va_end (args);
	std::abort ();
}

int main (int argc, char **argv)
{
	if (argc != 2 || (std::strcmp (argv [1], "compressed") != 0 && std::strcmp (argv [1], "uncompressed") != 0)) {
		std::fprintf (stderr, "Expected compressed or uncompressed\n");
		return 1;
	}
	bool is_compressed = std::strcmp (argv [1], "compressed") == 0;
	constexpr char name [] = "probe.dll";
	constexpr uint32_t name_length = sizeof (name) - 1;
	constexpr uint32_t image_size = 512;
	constexpr uint32_t compressed_size = sizeof (CompressedAssemblyHeader) + 1;
	constexpr uint32_t data_offset = sizeof (AssemblyStoreHeader) + sizeof (AssemblyStoreIndexEntry) +
		sizeof (AssemblyStoreEntryDescriptor) + sizeof (name_length) + name_length;
	AssemblyStoreHeader header { ASSEMBLY_STORE_MAGIC, ASSEMBLY_STORE_FORMAT_VERSION, 1, 1, sizeof (AssemblyStoreIndexEntry) };
	AssemblyStoreIndexEntry index { 42, 0, 0 };
	AssemblyStoreEntryDescriptor descriptor { 0, data_offset, is_compressed ? compressed_size : image_size, 0, 0, 0, 0 };
	CompressedAssemblyHeader compressed { COMPRESSED_DATA_MAGIC, 0, image_size };
	std::vector<uint8_t> payload (data_offset + descriptor.data_size, 0x5a);
	size_t offset = 0;
	auto write = [&] (const void *value, size_t size) {
		std::memcpy (payload.data () + offset, value, size);
		offset += size;
	};
	write (&header, sizeof (header));
	write (&index, sizeof (index));
	write (&descriptor, sizeof (descriptor));
	write (&name_length, sizeof (name_length));
	write (name, name_length);
	if (is_compressed) {
		write (&compressed, sizeof (compressed));
	}
	AssemblyStore::configure_from_payload (payload.data (), "probe");

	constexpr int worker_count = 16;
	std::atomic<int> ready {0};
	std::atomic<bool> start {false};
	std::vector<std::thread> threads;
	std::vector<void*> results (worker_count);
	std::vector<int64_t> sizes (worker_count);
	for (int i = 0; i < worker_count; ++i) {
		threads.emplace_back ([&, i] {
			ready.fetch_add (1);
			while (!start.load ()) {
				std::this_thread::yield ();
			}
			results [i] = AssemblyStore::open_assembly (name, sizes [i]);
		});
	}
	while (ready.load () != worker_count) {
		std::this_thread::yield ();
	}
	start.store (true);
	for (auto &thread : threads) {
		thread.join ();
	}
	for (int i = 0; i < worker_count; ++i) {
		if (results [i] == nullptr || results [i] != results [0] || sizes [i] != image_size) {
			std::fprintf (stderr, "Inconsistent assembly image (allocations=%d)\n", allocations.load ());
			return 1;
		}
	}
	if (is_compressed && decompressions.load () != 1) {
		std::fprintf (stderr, "Expected one decompression, got %d\n", decompressions.load ());
		return 1;
	}
	auto image = static_cast<uint8_t*>(results [0]);
	if (image [0] != 0x5a || image [image_size - 1] != 0x5a) {
		std::fprintf (stderr, "Assembly image has incorrect data\n");
		return 1;
	}
	return 0;
}

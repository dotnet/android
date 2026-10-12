#include <charconv>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <source_location>
#include <system_error>

#include <fcntl.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

#include <runtime-base/app-system-properties.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

// Run the production reader's failures in a child process, not the managed test runner.
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
	if (argc != 5) {
		Helpers::abort_application ("Expected object path, envelope offset, envelope size and property name");
	}
	size_t offset = 0;
	uint32_t size = 0;
	auto parsed_offset = std::from_chars (argv[2], argv[2] + std::strlen (argv[2]), offset);
	auto parsed_size = std::from_chars (argv[3], argv[3] + std::strlen (argv[3]), size);
	if (parsed_offset.ec != std::errc {} || *parsed_offset.ptr != '\0' ||
		parsed_size.ec != std::errc {} || *parsed_size.ptr != '\0' || size == 0 ||
		offset % alignof (AppSystemProperties::OffsetPair) != 0) {
		Helpers::abort_application ("Invalid bootstrap body range");
	}

	int descriptor = ::open (argv[1], O_RDONLY);
	if (descriptor < 0) {
		Helpers::abort_application ("Unable to open generated bootstrap object");
	}
	struct stat info;
	if (::fstat (descriptor, &info) != 0 || info.st_size <= 0 ||
		static_cast<uint64_t>(info.st_size) > std::numeric_limits<size_t>::max ()) {
		Helpers::abort_application ("Unable to size generated bootstrap object");
	}
	size_t length = static_cast<size_t>(info.st_size);
	if (offset > length || size > length - offset) {
		Helpers::abort_application ("Bootstrap body exceeds generated object");
	}
	void *mapping = ::mmap (nullptr, length, PROT_READ, MAP_PRIVATE, descriptor, 0);
	if (::close (descriptor) != 0 || mapping == MAP_FAILED) {
		Helpers::abort_application ("Unable to map generated bootstrap object");
	}
	auto blob = static_cast<const uint8_t*>(mapping) + offset;
	auto payload = BinaryBlobLoader::read_uncompressed (blob, size, "dotnet_android_nativeaot_bootstrap");
	if (payload.data != blob + 16 || static_cast<uint64_t>(payload.size) + 16 != size) {
		Helpers::abort_application ("Raw bootstrap payload must point directly into the mapped envelope");
	}
	auto body = payload.data;
	size = payload.size;
	AppSystemProperties::initialize (payload);

	size_t value_length = 0;
	const char *value = AppSystemProperties::lookup (argv[4], value_length);
	if (value == nullptr) {
		if (value_length != 0) {
			Helpers::abort_application ("Missing property must reset its length");
		}
		if (std::fputs ("missing", stdout) == EOF) {
			Helpers::abort_application ("Unable to write missing-property result");
		}
	} else {
		auto address = reinterpret_cast<uintptr_t>(value);
		auto start = reinterpret_cast<uintptr_t>(body);
		if (address < start || address - start >= size || value_length >= size - (address - start)) {
			Helpers::abort_application ("Property value must point directly into the mapped object body");
		}
		if (std::fwrite (value, 1, value_length, stdout) != value_length) {
			Helpers::abort_application ("Unable to write property value");
		}
	}
	if (::munmap (mapping, length) != 0) {
		Helpers::abort_application ("Unable to unmap generated bootstrap object");
	}
	return EXIT_SUCCESS;
}

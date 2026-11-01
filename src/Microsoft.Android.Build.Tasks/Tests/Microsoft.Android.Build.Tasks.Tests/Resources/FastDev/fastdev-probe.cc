#include <sys/types.h>
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>

#include <cassert>
#include <cerrno>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

#include <fastdev-test.hh>

const char *override_path = nullptr;
size_t assembly_size = 8;

namespace {
	constexpr int assembly_fd = 12345;
	constexpr char assembly_data [] = "abcdefgh";
	std::vector<ssize_t> reads;
	size_t read_index = 0;
	size_t bytes_read = 0;
	size_t close_count = 0;
	size_t open_count = 0;
	size_t free_count = 0;
	bool fail_open = false;
	bool fail_close = false;
	std::string warnings;

	auto test_openat (int, const char*, int flags) -> int
	{
		assert (flags == O_RDONLY);
		open_count++;
		if (fail_open) {
			errno = EACCES;
			return -1;
		}
		return assembly_fd;
	}

	auto test_read (int fd, void *buffer, size_t count) -> ssize_t
	{
		assert (fd == assembly_fd);
		assert (count == assembly_size - bytes_read);
		assert (count <= static_cast<size_t>(std::numeric_limits<ssize_t>::max ()));
		assert (read_index < reads.size ());
		ssize_t result = reads [read_index++];
		if (result < 0) {
			errno = static_cast<int>(-result);
			return -1;
		}
		assert (static_cast<size_t>(result) <= count);
		memcpy (buffer, assembly_data + bytes_read, static_cast<size_t>(result));
		bytes_read += static_cast<size_t>(result);
		// Successful reads and EOF do not clear errno.
		errno = ENOENT;
		return result;
	}

	auto test_close (int fd) -> int
	{
		assert (fd == assembly_fd);
		close_count++;
		// Even successful cleanup may overwrite the original read error.
		errno = EBADF;
		return fail_close ? -1 : 0;
	}

	auto track_free (void *buffer) -> void*
	{
		assert (buffer != nullptr);
		free_count++;
		return buffer;
	}
}

// Compile the actual reader and class header, substituting only OS/dependency boundaries.
#define openat test_openat
#define read test_read
#define close test_close
#define free(buffer) free (track_free (buffer))
#include <fastdev-assemblies.cc>
#undef free
#undef close
#undef read
#undef openat

void xamarin::android::log_warnf (int, const char *format, ...)
{
	char message [1024];
	va_list args;
	va_start (args, format);
	vsnprintf (message, sizeof(message), format, args);
	va_end (args);
	warnings += message;
	warnings += '\n';
	// Logging must not affect the saved syscall diagnostic.
	errno = EINVAL;
}

void xamarin::android::log_debugf (int, const char*, ...)
{}

void xamarin::android::log_infof (int, const char*, ...)
{}

int main (int argc, char **argv)
{
	assert (argc == 3);
	std::string scenario { argv [1] };
	override_path = argv [2];
	bool success = false;
	const char *diagnostic = nullptr;

	if (scenario == "complete" || scenario == "close-error") {
		reads = { 8 };
		success = true;
		fail_close = scenario == "close-error";
	} else if (scenario == "short") {
		reads = { 1, 2, 1, 4 };
		success = true;
	} else if (scenario == "interrupted") {
		reads = { -EINTR, -EINTR, 2, -EINTR, 3, -EINTR, 3 };
		success = true;
	} else if (scenario == "eof" || scenario == "partial-eof") {
		reads = scenario == "eof" ? std::vector<ssize_t> { 0 } : std::vector<ssize_t> { 3, -EINTR, 0 };
		diagnostic = scenario == "eof" ? "unexpected end of file after 0 of 8 bytes" : "unexpected end of file after 3 of 8 bytes";
	} else if (scenario == "error" || scenario == "partial-error") {
		reads = scenario == "error" ? std::vector<ssize_t> { -EIO } : std::vector<ssize_t> { 3, -EINTR, -EIO };
		fail_close = true;
		diagnostic = strerror (EIO);
	} else if (scenario == "empty" || scenario == "oversize") {
		assembly_size = scenario == "empty" ? 0 : std::numeric_limits<size_t>::max ();
		diagnostic = "Invalid FastDev assembly";
	} else if (scenario == "open-error") {
		fail_open = true;
		diagnostic = strerror (EACCES);
	} else {
		return 2;
	}

	int64_t size = -1;
	void *buffer = xamarin::android::FastDevAssemblies::open_assembly ("Test.dll", size);
	assert (read_index == reads.size ());
	assert (open_count == (scenario == "empty" || scenario == "oversize" ? 0uz : 1uz));
	assert (close_count == (open_count != 0 && !fail_open ? 1uz : 0uz));
	assert (free_count == (!success && !reads.empty () ? 1uz : 0uz));
	if (success) {
		assert (buffer != nullptr);
		assert (size == 8);
		assert (memcmp (buffer, assembly_data, 8) == 0);
		std::free (buffer);
		assert (fail_close || warnings.empty ());
	} else {
		assert (buffer == nullptr);
		assert (size == 0);
		assert (diagnostic != nullptr);
		assert (warnings.find (diagnostic) != std::string::npos);
	}
	if (fail_close) {
		assert (warnings.find ("Failed to close FastDev assembly") != std::string::npos);
		assert (warnings.find (strerror (EBADF)) != std::string::npos);
	}
	return 0;
}

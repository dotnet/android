#include <cassert>
#include <cerrno>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>

#include <fcntl.h>
#include <unistd.h>

#include <shared/log_functions.hh>
#include <shared/helpers.hh>

static const char *scenario;
static int opens;
static int closes;
static int warnings;

void xamarin::android::Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	assert (std::strcmp (scenario, "open-error") == 0 || std::strcmp (scenario, "read-error") == 0);
	assert (opens == 1 && warnings == 1);
	assert (closes == (std::strcmp (scenario, "open-error") == 0 ? 0 : 1));
	std::fprintf (stderr, "%s\n", message);
	std::printf ("opens=%d closes=%d warnings=%d\n", opens, closes, warnings);
	std::exit (EXIT_FAILURE);
}

void xamarin::android::log_warnf (LogCategories, const char *format, ...) noexcept
{
	++warnings;
	va_list args;
	va_start (args, format);
	std::vfprintf (stderr, format, args);
	va_end (args);
	std::fputc ('\n', stderr);
}

static FILE *open_maps (const char *path, const char *mode)
{
	assert (std::strcmp (path, "/proc/self/maps") == 0);
	assert (std::strcmp (mode, "r") == 0);
	++opens;
	if (std::strcmp (scenario, "open-error") == 0) {
		errno = EACCES;
		return nullptr;
	}
	if (std::strcmp (scenario, "real-maps") == 0) {
		return std::fopen (path, mode);
	}

	FILE *maps = std::tmpfile ();
	assert (maps != nullptr);
	if (std::strcmp (scenario, "empty") != 0) {
		const char *contents = std::strcmp (scenario, "houdini") == 0 || std::strcmp (scenario, "houdini-close-error") == 0
			? "1234-5678 r-xp 00000000 00:00 0 /system/lib/libhoudini.so\nanother mapping\n"
			: "1234-5678 r-xp 00000000 00:00 0 /system/lib/libc.so\n";
		assert (std::fputs (contents, maps) >= 0);
		std::rewind (maps);
	}
	return maps;
}

static char *read_maps (char *buffer, int size, FILE *maps)
{
	if (std::strcmp (scenario, "read-error") == 0) {
		errno = EIO;
		return nullptr;
	}
	return std::fgets (buffer, size, maps);
}

static int maps_error (FILE *maps)
{
	return std::strcmp (scenario, "read-error") == 0 ? 1 : std::ferror (maps);
}

static int close_maps (FILE *maps)
{
	int descriptor = fileno (maps);
	int result = std::fclose (maps);
	assert (result == 0);
	assert (fcntl (descriptor, F_GETFD) == -1 && errno == EBADF);
	++closes;
	if (std::strcmp (scenario, "close-error") == 0 || std::strcmp (scenario, "houdini-close-error") == 0) {
		errno = EIO;
		return EOF;
	}
	return result;
}

// Exercise the production ARM branch on any host without changing system-header architecture selection.
#define __arm__ 1
#define fopen open_maps
#define fgets read_maps
#define ferror maps_error
#define fclose close_maps
#include <runtime-base/cpu-arch-detect.cc>
#undef fclose
#undef ferror
#undef fgets
#undef fopen

int main (int argc, char **argv)
{
	assert (argc == 2);
	scenario = argv [1];
	bool open_error = std::strcmp (scenario, "open-error") == 0;
	bool read_error = std::strcmp (scenario, "read-error") == 0;
	bool close_error = std::strcmp (scenario, "close-error") == 0 || std::strcmp (scenario, "houdini-close-error") == 0;
	bool houdini = std::strcmp (scenario, "houdini") == 0 || std::strcmp (scenario, "houdini-close-error") == 0;
	uint16_t expected_cpu = houdini ? CPU_KIND_X86 : CPU_KIND_ARM;

	for (int i = 0; i < 100; ++i) {
		uint16_t built_for_cpu = CPU_KIND_UNKNOWN;
		uint16_t running_on_cpu = CPU_KIND_X86;
		bool is64bit = false;
		monodroid_detect_cpu_and_architecture (built_for_cpu, running_on_cpu, is64bit);
		assert (!open_error && !read_error);
		assert (built_for_cpu == CPU_KIND_ARM);
		assert (running_on_cpu == expected_cpu);
		assert (is64bit == (sizeof (void*) == 8));
		assert (opens == i + 1);
		assert (closes == (open_error ? 0 : opens));
		assert (warnings == (open_error || read_error || close_error ? opens : 0));
	}
	std::printf ("running=%u opens=%d closes=%d warnings=%d\n", expected_cpu, opens, closes, warnings);
}

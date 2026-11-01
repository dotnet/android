#include <cassert>
#include <cerrno>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>

#include <fcntl.h>
#include <shared/log_functions.hh>
#include <shared/helpers.hh>

static const char *scenario;
static int descriptor = -1;

void xamarin::android::Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	assert (std::strcmp (scenario, "open-error") == 0);
	std::fprintf (stderr, "%s\n", message);
	std::exit (EXIT_FAILURE);
}

void xamarin::android::log_warnf (LogCategories, const char *format, ...) noexcept
{
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
	if (std::strcmp (scenario, "open-error") == 0) {
		errno = EACCES;
		return nullptr;
	}
	FILE *maps = std::tmpfile ();
	assert (maps != nullptr);
	descriptor = fileno (maps);
	const char *contents = std::strcmp (scenario, "houdini") == 0
		? "1234-5678 r-xp 00000000 00:00 0 /system/lib/libhoudini.so\nanother mapping\n"
		: "1234-5678 r-xp 00000000 00:00 0 /system/lib/libc.so\n";
	assert (std::fputs (contents, maps) >= 0);
	std::rewind (maps);
	return maps;
}

// Exercise the production ARM branch on any host without changing system-header architecture selection.
#define __arm__ 1
#define fopen open_maps
#include <runtime-base/cpu-arch-detect.cc>
#undef fopen

int main (int argc, char **argv)
{
	assert (argc == 2);
	scenario = argv [1];
	uint16_t built_for_cpu = CPU_KIND_UNKNOWN;
	uint16_t running_on_cpu = CPU_KIND_X86;
	bool is64bit = false;
	monodroid_detect_cpu_and_architecture (built_for_cpu, running_on_cpu, is64bit);
	assert (std::strcmp (scenario, "open-error") != 0);
	assert (running_on_cpu == (std::strcmp (scenario, "houdini") == 0 ? CPU_KIND_X86 : CPU_KIND_ARM));
	assert (descriptor >= 0 && fcntl (descriptor, F_GETFD) == -1 && errno == EBADF);
}

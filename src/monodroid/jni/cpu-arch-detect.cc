#include <assert.h>
#include <stdio.h>
#include <string.h>

#if __APPLE__
#include <sys/types.h>
#include <sys/sysctl.h>
#include <mach/machine.h>
#elif _WIN32
#include <windows.h>
#endif

#include "cpu-arch.h"

#if __ANDROID__
#define BUF_SIZE 512

#if __arm__
static int
find_in_maps (const char *str)
{
	FILE *maps = fopen ("/proc/self/maps", "r");
	char *line;
	char  buf [BUF_SIZE];

	assert (str);
	
	if (!maps)
		return -1;

	while ((line = fgets (buf, BUF_SIZE, maps))) {
		if (strstr (line, str))
			return 1;
	}
	
	return 0;
}

static int
detect_houdini ()
{
	return find_in_maps ("libhoudini");
}
#endif
#endif // __ANDROID__

static unsigned char
is_64_bit ()
{
	return sizeof (char*) == 8;
}

static int
get_built_for_cpu_windows (unsigned short *built_for_cpu)
{
#if _WIN32
#if _M_AMD64 || _M_X64
	*built_for_cpu = CPU_KIND_X86_64;
#elif _M_IX86
	*built_for_cpu = CPU_KIND_X86;
#elif _M_ARM
	*built_for_cpu = CPU_KIND_ARM;
#else
	*built_for_cpu = CPU_KIND_UNKNOWN;
#endif
	return 1;
#else
	return 0;
#endif
}

static int
get_built_for_cpu_apple (unsigned short *built_for_cpu)
{
#if __APPLE__
#if __x86_64__
	*built_for_cpu = CPU_KIND_X86_64;
#elif __i386__
	*built_for_cpu = CPU_KIND_X86;
#else
	*built_for_cpu = CPU_KIND_UNKNOWN;
#endif
	return 1;
#else
	return 0;
#endif
}

static int
get_built_for_cpu_android (unsigned short *built_for_cpu)
{
	int retval = 1;
	
#if __arm__
	*built_for_cpu = CPU_KIND_ARM;
#elif __aarch64__
	*built_for_cpu = CPU_KIND_ARM64;
#elif __x86_64__
	*built_for_cpu = CPU_KIND_X86_64;
#elif __i386__
	*built_for_cpu = CPU_KIND_X86;
#elif __mips__
	*built_for_cpu = CPU_KIND_MIPS;
#else
	retval = 0;
#endif
	
	return retval;
}

static void
get_built_for_cpu (unsigned short *built_for_cpu)
{
	if (get_built_for_cpu_windows (built_for_cpu))
		return;

	if (get_built_for_cpu_apple (built_for_cpu))
		return;

	if (get_built_for_cpu_android (built_for_cpu))
		return;
	
	*built_for_cpu = CPU_KIND_UNKNOWN;
}

static int
get_running_on_cpu_windows (unsigned short *running_on_cpu)
{
#if _WIN32
	SYSTEM_INFO si;

	GetSystemInfo (&si);
	switch (si.wProcessorArchitecture) {
		case PROCESSOR_ARCHITECTURE_AMD64:
			*running_on_cpu = CPU_KIND_X86_64;
			break;

		case PROCESSOR_ARCHITECTURE_ARM:
			*running_on_cpu = CPU_KIND_ARM;
			break;

		case PROCESSOR_ARCHITECTURE_INTEL:
			*running_on_cpu = CPU_KIND_X86;
			break;

		default:
			*running_on_cpu = CPU_KIND_UNKNOWN;
			break;
	}
	
	return 1;
#else
	return 0;
#endif
}

static int
get_running_on_cpu_apple (unsigned short *running_on_cpu)
{
#if __APPLE__
	cpu_type_t cputype;
	size_t length;

	length = sizeof (cputype);
	sysctlbyname ("hw.cputype", &cputype, &length, nullptr, 0);
	switch (cputype) {
		case CPU_TYPE_X86:
			*running_on_cpu = CPU_KIND_X86;
			break;

		case CPU_TYPE_X86_64:
			*running_on_cpu = CPU_KIND_X86_64;
			break;

		default:
			*running_on_cpu = CPU_KIND_UNKNOWN;
			break;
	}
	
	return 1;
#else
	return 0;
#endif
}

static int
get_running_on_cpu_android (unsigned short *running_on_cpu)
{
	int retval = 1;
	
#if __arm__
	if (!detect_houdini ()) {
		*running_on_cpu = CPU_KIND_ARM;
	} else {
		/* If houdini is mapped in we're running on x86 */
		*running_on_cpu = CPU_KIND_X86;
	}
#elif __aarch64__
	*running_on_cpu = CPU_KIND_ARM64;
#elif __x86_64__
	*running_on_cpu = CPU_KIND_X86_64;
#elif __i386__
	*running_on_cpu = is_64_bit () ? CPU_KIND_X86_64 : CPU_KIND_X86;
#elif __mips__
	*running_on_cpu = CPU_KIND_MIPS;
#else
	retval = 0;
#endif

	return retval = 1;
}

static void
get_running_on_cpu (unsigned short *running_on_cpu)
{
	if (get_running_on_cpu_windows (running_on_cpu))
		return;

	if (get_running_on_cpu_apple (running_on_cpu))
		return;

	if (get_running_on_cpu_android (running_on_cpu))
		return;

	*running_on_cpu = CPU_KIND_UNKNOWN;
}

MONO_API void
_monodroid_detect_cpu_and_architecture (unsigned short *built_for_cpu, unsigned short *running_on_cpu, unsigned char *is64bit)
{
	assert (built_for_cpu);
	assert (running_on_cpu);
	assert (is64bit);

	*is64bit = is_64_bit ();
	get_built_for_cpu (built_for_cpu);
	get_running_on_cpu (running_on_cpu);
}

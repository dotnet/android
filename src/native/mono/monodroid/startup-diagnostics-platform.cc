#include "startup-diagnostics-internal.hh"
#include "startup-diagnostics-platform.hh"

#include <android/log.h>
#include <fcntl.h>
#include <time.h>
#include <unistd.h>

using namespace xamarin::android::startup;

namespace {
	template<size_t Size> std::string_view read_identity_file (const char* path, std::array<char, Size>& buffer) noexcept
	{
		int fd = open (path, O_RDONLY | O_CLOEXEC);
		if (fd < 0)
			return {};
		// A failed/interrupted/oversized read is unavailable evidence; never retry startup diagnostics.
		ssize_t count = read (fd, buffer.data (), buffer.size ());
		close (fd);
		if (count <= 0 || static_cast<size_t> (count) == buffer.size ())
			return {};
		return {buffer.data (), static_cast<size_t> (count)};
	}

	Identity read_identity () noexcept
	{
		PreserveErrno guard;
		Identity result;
		pid_t pid = getpid ();
		if (pid > 0)
			result.pid = static_cast<uint64_t> (pid);
		result.uid = static_cast<uint64_t> (getuid ());
		std::array<char, 4096> stat {};
		result.start_ticks = parse_start_ticks (read_identity_file ("/proc/self/stat", stat), result.pid);
		std::array<char, 64> boot {};
		auto id = read_identity_file ("/proc/sys/kernel/random/boot_id", boot);
		if (!id.empty () && id.back () == '\n')
			id.remove_suffix (1);
		if (valid_boot_id (id)) {
			for (size_t i = 0; i < id.size (); ++i)
				result.boot_id [i] = id [i];
		}
		return result;
	}

	uint64_t read_thread_id () noexcept
	{
		PreserveErrno guard;
		pid_t tid = gettid ();
		return tid > 0 ? static_cast<uint64_t> (tid) : 0;
	}

	std::optional<uint64_t> read_clock (clockid_t clock) noexcept
	{
		timespec value {};
		if (clock_gettime (clock, &value) != 0)
			return {};
		return nanoseconds (value.tv_sec, value.tv_nsec);
	}

	ClockSample read_clocks () noexcept
	{
		PreserveErrno guard;
		ClockSample sample;
		sample.monotonic_before = read_clock (CLOCK_MONOTONIC);
		sample.realtime = read_clock (CLOCK_REALTIME);
		sample.monotonic_after = read_clock (CLOCK_MONOTONIC);
		return sample;
	}

	int write_logcat (const char* record) noexcept
	{
		PreserveErrno guard;
		return __android_log_write (ANDROID_LOG_INFO, "mono-startup-meta", record);
	}
}

const Platform xamarin::android::startup::android_platform {
	read_identity, read_thread_id, read_clocks, write_logcat
};

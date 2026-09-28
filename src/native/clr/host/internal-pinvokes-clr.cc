#include <host/gc-bridge.hh>
#include <host/host.hh>
#include <host/os-bridge.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/internal-pinvokes.hh>

using namespace xamarin::android;

managed_timing_sequence* monodroid_timing_start (const char *message)
{
	if (!FastTiming::enabled ()) [[likely]] {
		return nullptr;
	}

	managed_timing_sequence *ret = Host::get_timing ().get_available_sequence ();
	if (message != nullptr) {
		log_write (LOG_TIMING, LogLevel::Info, message);
	}
	ret->start = FastTiming::get_time ();
	return ret;
}

void monodroid_timing_stop (managed_timing_sequence *sequence, const char *message)
{
	constexpr std::string_view DEFAULT_MESSAGE { "Managed Timing" };
	if (sequence == nullptr) {
		return;
	}

	sequence->end = FastTiming::get_time ();
	Timing::info (sequence, message == nullptr ? DEFAULT_MESSAGE.data () : message);
	Host::get_timing ().release_sequence (sequence);
}

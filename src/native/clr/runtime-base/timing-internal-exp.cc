#include <runtime-base/timing-internal-exp.hh>
#include <runtime-base/util.hh>

namespace xamarin::android::exp {
	FastTiming internal_timing;
}

using namespace xamarin::android::exp;

void
FastTiming::really_initialize (bool log_immediately) noexcept
{
	internal_timing.configure_for_use ();
	is_enabled = true;
	immediate_logging = log_immediately;

	// TLS variables are initialized on first use, do it here so that we can have
	// the overhead out of mind later.
	open_sequences.push (0);
	open_sequences.pop ();

	if (immediate_logging) {
		return;
	}

	log_write (LOG_TIMING, LogLevel::Info, "[2/1] To get timing results, send the mono.android.app.DUMP_TIMING_DATA intent to the application");
}

#include <runtime-base/timing.hh>
#include <runtime-base/timing-internal.hh>

using namespace xamarin::android;

void timing_point::mark ()
{
	FastTiming::get_time (sec, ns);
}

timing_diff::timing_diff (const timing_period &period)
{
	FastTiming::calculate_interval (period.start, period.end, *this);
}

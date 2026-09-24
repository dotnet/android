#include <runtime-base/internal-pinvokes.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

namespace {
	[[gnu::noreturn]]
	void pinvoke_unreachable (std::source_location sloc = std::source_location::current ())
	{
		Helpers::abort_application (
			LOG_DEFAULT,
			"The p/invoke is not implemented. This is a stub and should not be called."sv,
			true, // log_location
			sloc
		);
	}
}

const char* _monodroid_lookup_replacement_type ([[maybe_unused]] const char *jniSimpleReference)
{
	pinvoke_unreachable ();
}

const JniRemappingReplacementMethod* _monodroid_lookup_replacement_method_info (
	[[maybe_unused]] const char *jniSourceType,
	[[maybe_unused]] const char *jniMethodName,
	[[maybe_unused]] const char *jniMethodSignature)
{
	pinvoke_unreachable ();
}

managed_timing_sequence* monodroid_timing_start ([[maybe_unused]] const char *message)
{
	pinvoke_unreachable ();
}

void monodroid_timing_stop (
	[[maybe_unused]] managed_timing_sequence *sequence,
	[[maybe_unused]] const char *message)
{
	pinvoke_unreachable ();
}

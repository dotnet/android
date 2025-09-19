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

const char* clr_typemap_managed_to_java (
	[[maybe_unused]] const char *typeName,
	[[maybe_unused]] const uint8_t *mvid) noexcept
{
	pinvoke_unreachable ();
}

bool clr_typemap_java_to_managed (
	[[maybe_unused]] const char *java_type_name,
	[[maybe_unused]] char const** assembly_name,
	[[maybe_unused]]  uint32_t *managed_type_token_id) noexcept
{
	pinvoke_unreachable ();
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

void _monodroid_weak_gref_delete (
	[[maybe_unused]] jobject handle,
	[[maybe_unused]] char type,
	[[maybe_unused]] const char *threadName,
	[[maybe_unused]] int threadId,
	[[maybe_unused]] const char *from,
	[[maybe_unused]] int from_writable)
{
	pinvoke_unreachable ();
}

void* _monodroid_timezone_get_default_id ()
{
	pinvoke_unreachable ();
}

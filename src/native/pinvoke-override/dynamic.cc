#include "logger.hh"

#define PINVOKE_OVERRIDE_INLINE [[gnu::noinline]]
#include "pinvoke-override-api.hh"

using namespace xamarin::android;

[[gnu::flatten]]
void*
PinvokeOverride::monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name)
{
	log_info (LOG_ASSEMBLY, __PRETTY_FUNCTION__);
	log_info (LOG_ASSEMBLY, "library_name == '%s'; entrypoint_name == '%s'", library_name, entrypoint_name);

	// TODO: implement
	return nullptr;
}

#include "debug.hh"

using namespace xamarin::android;

extern "C" const char *__get_debug_mono_log_property (void)
{
	return Debug::DEBUG_MONO_LOG_PROPERTY.data ();
}

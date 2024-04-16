#include "debug.hh"
#include "shared-constants.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

extern "C" const char *__get_debug_mono_log_property (void)
{
	return static_cast<const char*> (SharedConstants::DEBUG_MONO_LOG_PROPERTY.data ());
}

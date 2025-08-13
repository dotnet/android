#include <runtime-base/runtime-environment.hh>

#include "osbridge.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

auto RuntimeEnvironment::get_jnienv () noexcept -> JNIEnv*
{
	return OSBridge::ensure_jnienv ();
}

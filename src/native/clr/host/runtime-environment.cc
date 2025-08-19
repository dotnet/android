#include <runtime-base/runtime-environment.hh>
#include <host/os-bridge.hh>

using namespace xamarin::android;

auto RuntimeEnvironment::get_jnienv () noexcept -> JNIEnv*
{
	return OSBridge::ensure_jnienv ();
}

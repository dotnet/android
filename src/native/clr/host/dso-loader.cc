#include <runtime-base/dso-loader.hh>

#include <host/os-bridge.hh>

using namespace xamarin::android;

auto DsoLoader::get_jnienv () noexcept -> JNIEnv*
{
	return OSBridge::ensure_jnienv ();
}

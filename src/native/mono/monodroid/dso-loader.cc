#include <runtime-base/dso-loader.hh>

#include "osbridge.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

auto DsoLoader::get_jnienv () noexcept -> JNIEnv*
{
	return OSBridge::ensure_jnienv ();
}

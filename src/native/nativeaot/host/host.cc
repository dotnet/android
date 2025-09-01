#include <host/host.hh>

using namespace xamarin::android;

auto Host::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	jvm = vm;

	return JNI_VERSION_1_6;
}

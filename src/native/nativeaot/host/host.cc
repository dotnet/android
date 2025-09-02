#include <host/host.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

auto Host::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	log_debug (LOG_ASSEMBLY, "{}", __PRETTY_FUNCTION__);

	jvm = vm;

	return JNI_VERSION_1_6;
}

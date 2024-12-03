#include <host/host.hh>
#include <host/host-jni.hh>
#include <runtime-base/android-system.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

auto Host::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	log_write (LOG_DEFAULT, LogLevel::Info, "Host init");

	AndroidSystem::init_max_gref_count ();
	return JNI_VERSION_1_6;
}

#include <host/bridge-processing.hh>
#include <host/gc-bridge.hh>
#include <host/host-nativeaot.hh>
#include <host/os-bridge.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

auto HostCommon::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	log_warn (LOG_ASSEMBLY, "{}", __PRETTY_FUNCTION__);

	jvm = vm;

	JNIEnv *env = nullptr;
    vm->GetEnv ((void**)&env, JNI_VERSION_1_6);
    OSBridge::initialize_on_onload (vm, env);
    GCBridge::initialize_on_onload (env);

	return JNI_VERSION_1_6;
}

void Host::OnInit () noexcept
{
	log_warn (LOG_ASSEMBLY, "{}", __PRETTY_FUNCTION__);
	Logger::init_logging_categories ();

	JNIEnv *env = OSBridge::ensure_jnienv ();
	jclass runtimeClass = env->FindClass ("mono/android/Runtime");
	OSBridge::initialize_on_runtime_init (env, runtimeClass);
	GCBridge::initialize_on_runtime_init (env, runtimeClass);
	BridgeProcessing::naot_initialize_on_runtime_init (env);
}

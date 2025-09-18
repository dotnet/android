#include <host/bridge-processing.hh>
#include <host/gc-bridge.hh>
#include <host/host-environment-naot.hh>
#include <host/host-nativeaot.hh>
#include <host/os-bridge.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

using JniOnLoadHandler = jint (*) (JavaVM *vm, void *reserved);

//
// These external functions are generated during application build (see obj/${CONFIG}/${FRAMEWORK}-android/${RID}/android/jni_init_funcs*.ll)
//
extern "C" {
		extern const uint32_t __jni_on_load_handler_count;
		extern const JniOnLoadHandler __jni_on_load_handlers[];
		extern const char* __jni_on_load_handler_names[];
}

auto HostCommon::Java_JNI_OnLoad (JavaVM *vm, void *reserved) noexcept -> jint
{
	Logger::init_logging_categories ();
	HostEnvironment::init ();
	jvm = vm;

	JNIEnv *env = nullptr;
	vm->GetEnv ((void**)&env, JNI_VERSION_1_6);
	OSBridge::initialize_on_onload (vm, env);
	GCBridge::initialize_on_onload (env);
	AndroidSystem::init_max_gref_count ();

	if (__jni_on_load_handler_count > 0) {
		for (uint32_t i = 0; i < __jni_on_load_handler_count; i++) {
			log_debug (
				LOG_ASSEMBLY,
				"Calling JNI on-load init func '{}' ({:p})",
				optional_string (__jni_on_load_handler_names[i]),
				reinterpret_cast<void*>(__jni_on_load_handlers[i])
			);
			__jni_on_load_handlers[i] (vm, reserved);
		}
	}

	return JNI_VERSION_1_6;
}

void Host::OnInit () noexcept
{
	JNIEnv *env = OSBridge::ensure_jnienv ();
	jclass runtimeClass = env->FindClass ("mono/android/Runtime");
	OSBridge::initialize_on_runtime_init (env, runtimeClass);
	GCBridge::initialize_on_runtime_init (env, runtimeClass);
	BridgeProcessing::naot_initialize_on_runtime_init (env);
}

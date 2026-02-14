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
				"Calling JNI on-load init func '%s' (%p)",
				optional_string (__jni_on_load_handler_names[i]),
				reinterpret_cast<void*>(__jni_on_load_handlers[i])
			);
			__jni_on_load_handlers[i] (vm, reserved);
		}
	}

	return JNI_VERSION_1_6;
}

// Be VERY careful with what we do here - the managed runtime is not fully initialized
// at the point this method is called.
void Host::OnInit (jstring language, jstring filesDir, jstring cacheDir, JnienvInitializeArgs *initArgs) noexcept
{
	abort_if_invalid_pointer_argument (initArgs, "initArgs");

	JNIEnv *env = OSBridge::ensure_jnienv ();
	jclass runtimeClass = env->FindClass ("mono/android/Runtime");

	jstring_wrapper language_js (env, language);
	jstring_wrapper files_dir (env, filesDir);
	jstring_wrapper cache_dir (env, cacheDir);
	AndroidSystem::set_primary_override_dir (files_dir);
	HostEnvironment::setup_environment (language_js, files_dir, cache_dir);
	Logger::init_reference_logging (AndroidSystem::get_primary_override_dir ());

	OSBridge::initialize_on_runtime_init (env, runtimeClass);
	GCBridge::initialize_on_runtime_init (env, runtimeClass);
	BridgeProcessing::naot_initialize_on_runtime_init (env);

	// We expect the struct to be initialized by the managed land the way it sees fit, we set only the
	// fields we support.
	initArgs->logCategories = log_categories;
}

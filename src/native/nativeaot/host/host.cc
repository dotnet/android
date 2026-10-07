#include <host/host-common.hh>
#include <host/host-environment-naot.hh>
#include <host/host-nativeaot.hh>
#include <host/os-bridge.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/binary-blob-loader.hh>
#include <runtime-base/app-system-properties.hh>
#include <shared/log_functions.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

auto HostCommon::Java_JNI_OnLoad (JavaVM *vm, void*) noexcept -> jint
{
	abort_if_invalid_pointer_argument (vm, "vm");
	JNIEnv *env = nullptr;
	jint result = vm->GetEnv (reinterpret_cast<void**>(&env), JNI_VERSION_1_6);
	abort_unless (result == JNI_OK && env != nullptr, "Unable to get JNI environment for NativeAOT startup");
	AppSystemProperties::initialize (env);

	HostCommon::init_logging_categories ();
	HostEnvironment::init ();
	jvm = vm;

	OSBridge::initialize_on_onload (vm);
	AndroidSystem::init_max_gref_count ();

	return JNI_VERSION_1_6;
}

// Be VERY careful with what we do here - the managed runtime is not fully initialized
// at the point this method is called.
void Host::OnInit (jstring_wrapper &language, jstring_wrapper &files_dir, jstring_wrapper &cache_dir, JnienvInitializeArgs *initArgs) noexcept
{
	abort_if_invalid_pointer_argument (initArgs, "initArgs");

	JNIEnv *env = OSBridge::ensure_jnienv ();

	AndroidSystem::set_primary_override_dir (files_dir);
	HostEnvironment::setup_environment (language, files_dir, cache_dir);

	// We expect the struct to be initialized by the managed land the way it sees fit, we set only the
	// fields we support.
	// NativeAOT initializes Mono.Android's common JNI state before creating the JniRuntime,
	// so the Java peer marker class must be provided by the host instead of being looked
	// up later from mono.android.Runtime static fields like MonoVM/CoreCLR.
	jclass lrefIGCUserPeer = env->FindClass ("mono/android/IGCUserPeer");
	if (lrefIGCUserPeer == nullptr) [[unlikely]] {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		abort_unless (false, "Failed to load mono/android/IGCUserPeer class");
	}

	jclass lrefGCUserPeerable = env->FindClass ("net/dot/jni/GCUserPeerable");
	if (lrefGCUserPeerable == nullptr) [[unlikely]] {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		abort_unless (false, "Failed to load net/dot/jni/GCUserPeerable class");
	}

	initArgs->logCategories = log_categories;
	initArgs->grefGcThreshold = static_cast<int>(AndroidSystem::get_gref_gc_threshold ());
	initArgs->maxGrefCount = static_cast<int>(AndroidSystem::get_max_gref_count ());
	initArgs->grefIGCUserPeer = env->NewGlobalRef (lrefIGCUserPeer);
	if (initArgs->grefIGCUserPeer == nullptr) [[unlikely]] {
		if (env->ExceptionCheck ()) {
			env->ExceptionDescribe ();
			env->ExceptionClear ();
		}
		abort_unless (false, "Failed to create a global reference for mono/android/IGCUserPeer");
	}
	initArgs->grefGCUserPeerable = env->NewGlobalRef (lrefGCUserPeerable);
	if (initArgs->grefGCUserPeerable == nullptr) [[unlikely]] {
		if (env->ExceptionCheck ()) {
			env->ExceptionDescribe ();
			env->ExceptionClear ();
		}
		abort_unless (false, "Failed to create a global reference for net/dot/jni/GCUserPeerable");
	}
	static BinaryBlobPayload remapping = {};
	remapping = BinaryBlobLoader::load ("xa_jni_remapping");
	initArgs->jniRemappingData = &remapping;

	env->DeleteLocalRef (lrefIGCUserPeer);
	env->DeleteLocalRef (lrefGCUserPeerable);
}

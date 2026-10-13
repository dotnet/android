#include <host/host-common.hh>
#include <host/gc-bridge.hh>
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
	initArgs->logCategories = log_categories;
	initArgs->grefGcThreshold = static_cast<int>(AndroidSystem::get_gref_gc_threshold ());
	initArgs->maxGrefCount = static_cast<int>(AndroidSystem::get_max_gref_count ());
	initArgs->initializeGCBridge = &GCBridge::initialize_callback;
	initArgs->grefIGCUserPeer = create_igc_user_peer_global_ref (env);
	static BinaryBlobPayload remapping = {};
	remapping = load_nativeaot_remapping ();
	initArgs->jniRemappingData = remapping.data == nullptr ? nullptr : &remapping;
}

auto Host::create_igc_user_peer_global_ref (JNIEnv *env) noexcept -> jobject
{
	// NativeAOT initializes Mono.Android's common JNI state before creating the JniRuntime,
	// so the Java peer marker class must be provided by the host instead of being looked
	// up later from mono.android.Runtime static fields like MonoVM/CoreCLR.
	jclass lrefIGCUserPeer = env->FindClass ("mono/android/IGCUserPeer");
	if (lrefIGCUserPeer == nullptr) [[unlikely]] {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		abort_unless (false, "Failed to load mono/android/IGCUserPeer class");
	}

	jobject grefIGCUserPeer = env->NewGlobalRef (lrefIGCUserPeer);
	if (grefIGCUserPeer == nullptr) [[unlikely]] {
		if (env->ExceptionCheck ()) {
			env->ExceptionDescribe ();
			env->ExceptionClear ();
		}
		abort_unless (false, "Failed to create a global reference for mono/android/IGCUserPeer");
	}
	env->DeleteLocalRef (lrefIGCUserPeer);
	return grefIGCUserPeer;
}

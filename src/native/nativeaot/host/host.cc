#include <host/gc-bridge.hh>
#include <host/host-nativeaot.hh>
#include <host/os-bridge.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/app-bootstrap-properties.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

auto HostCommon::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	JNIEnv *env = nullptr;
	vm->GetEnv ((void**)&env, JNI_VERSION_1_6);
	AppBootstrapProperties::initialize (env);

	Logger::init_logging_categories ();
	jvm = vm;

	OSBridge::initialize_on_onload (vm, env);
	GCBridge::initialize_on_onload (env);
	AndroidSystem::init_max_gref_count ();

	return JNI_VERSION_1_6;
}

// Be VERY careful with what we do here - the managed runtime is not fully initialized
// at the point this method is called.
void Host::OnInit ([[maybe_unused]] jstring_wrapper &language, jstring_wrapper &files_dir, [[maybe_unused]] jstring_wrapper &cache_dir, JnienvInitializeArgs *initArgs) noexcept
{
	abort_if_invalid_pointer_argument (initArgs, "initArgs");

	JNIEnv *env = OSBridge::ensure_jnienv ();
	jclass runtimeClass = env->FindClass ("mono/android/Runtime");

	AndroidSystem::set_primary_override_dir (files_dir);
	Logger::init_reference_logging (AndroidSystem::get_primary_override_dir ());

	OSBridge::initialize_on_runtime_init (env, runtimeClass);
	GCBridge::initialize_on_runtime_init (env, runtimeClass);

	// We expect the struct to be initialized by the managed land the way it sees fit, we set only the
	// fields we support.
	// NativeAOT initializes Mono.Android's common JNI state before creating the JniRuntime,
	// so the Java peer marker classes must be provided by the host instead of being looked
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
	auto remapping_data = AppBootstrapProperties::remapping_data ();
	initArgs->jniRemappingData = remapping_data.data ();
	initArgs->jniRemappingDataLength = static_cast<int32_t>(remapping_data.size ());
	initArgs->grefGcThreshold = static_cast<int>(AndroidSystem::get_gref_gc_threshold ());
	initArgs->maxGrefCount = static_cast<int>(AndroidSystem::get_max_gref_count ());
	initArgs->grefIGCUserPeer = env->NewGlobalRef (lrefIGCUserPeer);
	initArgs->grefGCUserPeerable = env->NewGlobalRef (lrefGCUserPeerable);
	initArgs->grefLogPath = Logger::gref_log_path ();
	initArgs->lrefLogPath = Logger::lref_log_path ();
	initArgs->referenceLogDirectory = Logger::reference_log_directory ();
	initArgs->lightGref = Logger::light_gref_enabled () ? 1 : 0;
	initArgs->lightLref = Logger::light_lref_enabled () ? 1 : 0;
	initArgs->grefToLogcat = Logger::gref_to_logcat () ? 1 : 0;
	initArgs->lrefToLogcat = Logger::lref_to_logcat () ? 1 : 0;

	env->DeleteLocalRef (lrefIGCUserPeer);
	env->DeleteLocalRef (lrefGCUserPeerable);
}

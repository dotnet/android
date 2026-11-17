#include <cerrno>
#include <cstdlib>
#include <cstring>

#include <managed-interface.hh>

#include <host/host-common.hh>
#include <host/gc-bridge.hh>
#include <host/host-environment.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/binary-blob-loader.hh>
#include <runtime-base/app-system-properties.hh>
#include <runtime-base/jni-wrappers.hh>
#include <shared/log_functions.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

extern "C" {
	[[gnu::visibility("hidden")]] extern const uint8_t dotnet_android_nativeaot_bootstrap[];
	[[gnu::visibility("hidden")]] extern const uint32_t dotnet_android_nativeaot_bootstrap_size;
	[[gnu::weak, gnu::visibility("hidden")]] extern const uint8_t dotnet_android_nativeaot_remapping_data[];
	[[gnu::weak, gnu::visibility("hidden")]] extern const uint32_t dotnet_android_nativeaot_remapping_data_size;
}

namespace {
	auto load_nativeaot_remapping () noexcept -> const BinaryBlobPayload*
	{
		static BinaryBlobPayload payload = {};
		static bool initialized = false;
		if (!initialized) {
			if (dotnet_android_nativeaot_remapping_data != nullptr && &dotnet_android_nativeaot_remapping_data_size != nullptr) {
				payload = BinaryBlobLoader::read_uncompressed (
					dotnet_android_nativeaot_remapping_data, dotnet_android_nativeaot_remapping_data_size, "dotnet_android_nativeaot_remapping_data");
			} else if (dotnet_android_nativeaot_remapping_data != nullptr || &dotnet_android_nativeaot_remapping_data_size != nullptr) {
				Helpers::abort_application ("Incomplete NativeAOT remapping symbols");
			}
			initialized = true;
		}
		return payload.data == nullptr ? nullptr : &payload;
	}

	jstring_wrapper duplicate_local_reference (JNIEnv *env, jstring value) noexcept
	{
		if (value == nullptr) {
			return jstring_wrapper (env);
		}

		auto local_ref = reinterpret_cast<jstring> (env->NewLocalRef (value));
		if (local_ref != nullptr) [[likely]] {
			return jstring_wrapper (env, local_ref);
		}

		if (env->ExceptionCheck ()) {
			env->ExceptionDescribe ();
			env->ExceptionClear ();
		}
		Helpers::abort_application ("Failed to duplicate a JNI string reference for NativeAOT initialization");
	}

	auto create_igc_user_peer_global_ref (JNIEnv *env) noexcept -> jobject
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
}

// Selected by the application link's DT_INIT, before its NativeAOT/C++ .init_array.
// Dependencies have already run their constructors; this routine must not need JNI or managed code.
extern "C" [[gnu::visibility("hidden")]]
void NativeAOT_InitializeEnvironment () noexcept
{
	AppSystemProperties::initialize (BinaryBlobLoader::read_uncompressed (
		dotnet_android_nativeaot_bootstrap, dotnet_android_nativeaot_bootstrap_size, "dotnet_android_nativeaot_bootstrap"));
	for (const auto &variable : AppSystemProperties::environment ()) {
		if (::setenv (variable.name, variable.value, 1) != 0) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Unable to set NativeAOT environment variable '%s': %s", variable.name, std::strerror (errno));
		}
	}
}

auto AndroidSystem::lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*
{
	return AppSystemProperties::lookup (name, value_len);
}

extern "C" [[gnu::visibility("default")]]
auto NativeAOT_JNI_OnLoad (JavaVM *vm, void*, JnienvInitializeArgs *initArgs) -> int
{
	abort_if_invalid_pointer_argument (initArgs, "initArgs");
	abort_if_invalid_pointer_argument (vm, "vm");
	JNIEnv *env = nullptr;
	jint result = vm->GetEnv (reinterpret_cast<void**>(&env), JNI_VERSION_1_6);
	abort_unless (result == JNI_OK && env != nullptr, "Unable to get JNI environment for NativeAOT startup");
	HostCommon::init_logging_categories ();
	for (AppSystemProperties::Entry const& property : AppSystemProperties::entries ()) {
		HostEnvironment::set_system_property (property.name, property.value);
	}

	AndroidSystem::init_max_gref_count ();

	initArgs->grefGcThreshold = static_cast<int>(AndroidSystem::get_gref_gc_threshold ());
	initArgs->maxGrefCount = static_cast<int>(AndroidSystem::get_max_gref_count ());
	return JNI_VERSION_1_6;
}

// Be VERY careful with what we do here - the managed runtime is not fully initialized
// at the point this method is called.
extern "C" void NativeAOT_OnInit (JNIEnv *env, jstring language, jstring filesDir, jstring cacheDir, JnienvInitializeArgs *initArgs)
{
	abort_if_invalid_pointer_argument (env, "env");
	abort_if_invalid_pointer_argument (initArgs, "initArgs");

	// Give the wrappers their own references; the caller still needs its borrowed arguments.
	auto language_js = duplicate_local_reference (env, language);
	auto files_dir = duplicate_local_reference (env, filesDir);
	auto cache_dir = duplicate_local_reference (env, cacheDir);

	AndroidSystem::set_primary_override_dir (files_dir);
	HostEnvironment::setup_environment (language_js, files_dir, cache_dir);

	// We expect the struct to be initialized by the managed land the way it sees fit, we set only the
	// fields we support.
	initArgs->logCategories = log_categories;
	initArgs->grefGcThreshold = static_cast<int>(AndroidSystem::get_gref_gc_threshold ());
	initArgs->maxGrefCount = static_cast<int>(AndroidSystem::get_max_gref_count ());
	initArgs->initializeGCBridge = &GCBridge::initialize_callback;
	initArgs->grefIGCUserPeer = create_igc_user_peer_global_ref (env);
	initArgs->jniRemappingData = load_nativeaot_remapping ();
}

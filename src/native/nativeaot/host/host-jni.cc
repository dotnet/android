#include <jni.h>

#include <host/host-jni.hh>
#include <host/host-nativeaot.hh>
#include <host/os-bridge.hh>
#include <runtime-base/logger.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

namespace {
	jstring duplicate_local_reference (JNIEnv *env, jstring value) noexcept
	{
		if (value == nullptr) {
			return nullptr;
		}

		auto local_ref = reinterpret_cast<jstring> (env->NewLocalRef (value));
		if (local_ref != nullptr) [[likely]] {
			return local_ref;
		}

		if (env->ExceptionCheck ()) {
			env->ExceptionDescribe ();
			env->ExceptionClear ();
		}
		Helpers::abort_application ("Failed to duplicate a JNI string reference for NativeAOT initialization");
	}
}

auto XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved) -> int
{
	return Host::Java_JNI_OnLoad (vm, reserved);
}

void XA_Host_NativeAOT_OnInit (jstring language, jstring filesDir, jstring cacheDir, JnienvInitializeArgs *initArgs)
{
	JNIEnv *env = OSBridge::ensure_jnienv ();

	// JNI method arguments are borrowed and must remain valid after Host::OnInit returns.
	// Pass duplicates because Host::OnInit takes ownership of the references it receives.
	jstring language_ref = duplicate_local_reference (env, language);
	jstring files_dir_ref = duplicate_local_reference (env, filesDir);
	jstring cache_dir_ref = duplicate_local_reference (env, cacheDir);
	Host::OnInit (language_ref, files_dir_ref, cache_dir_ref, initArgs);
}

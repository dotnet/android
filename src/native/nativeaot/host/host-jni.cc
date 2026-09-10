#include <jni.h>

#include <host/host-jni.hh>
#include <host/host-nativeaot.hh>
#include <host/os-bridge.hh>
#include <runtime-base/jni-wrappers.hh>
#include <runtime-base/logger.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

namespace {
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
}

auto XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved) -> int
{
	return Host::Java_JNI_OnLoad (vm, reserved);
}

void XA_Host_NativeAOT_OnInit (jstring language, jstring filesDir, jstring cacheDir, JnienvInitializeArgs *initArgs)
{
	JNIEnv *env = OSBridge::ensure_jnienv ();

	// Give the wrappers their own references; the caller still needs its borrowed arguments.
	auto language_js = duplicate_local_reference (env, language);
	auto files_dir = duplicate_local_reference (env, filesDir);
	auto cache_dir = duplicate_local_reference (env, cacheDir);
	Host::OnInit (language_js, files_dir, cache_dir, initArgs);
}

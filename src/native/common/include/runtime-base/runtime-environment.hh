#pragma once

#include <jni.h>

namespace xamarin::android {
	class RuntimeEnvironment
	{
	public:
		static void initialize (JavaVM *vm) noexcept;
		static auto get_java_vm () noexcept -> JavaVM* { return jvm; }
#if !defined (XA_HOST_NATIVEAOT)
		// NativeAOT startup receives its JNIEnv* from the managed entry point; only CoreCLR attaches here.
		static auto get_jnienv () noexcept -> JNIEnv*;
#endif // !defined (XA_HOST_NATIVEAOT)
		static void register_thread_detach (JNIEnv *env) noexcept;

	private:
		static void create_jni_env_key () noexcept;
		static void detach_current_thread (void *value) noexcept;

		static inline JavaVM *jvm = nullptr;
	};
}

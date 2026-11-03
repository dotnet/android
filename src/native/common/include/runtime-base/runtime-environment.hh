#pragma once

#include <jni.h>

namespace xamarin::android {
	class RuntimeEnvironment
	{
	public:
		static void initialize (JavaVM *vm) noexcept;
		static auto get_java_vm () noexcept -> JavaVM* { return jvm; }
		static auto get_jnienv () noexcept -> JNIEnv*;

	private:
		static void create_jni_env_key () noexcept;
		static void register_thread_detach (JNIEnv *env) noexcept;
		static void detach_current_thread (void *value) noexcept;

		static inline JavaVM *jvm = nullptr;
	};
}

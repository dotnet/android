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
		static inline JavaVM *jvm = nullptr;
	};
}

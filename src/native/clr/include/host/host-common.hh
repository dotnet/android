#pragma once

#include <jni.h>

namespace xamarin::android {
	// Common interfaces shared between the CoreCLR and NativeAOT runtimes
	class HostCommon
	{
	public:
		static auto Java_JNI_OnLoad (JavaVM *vm, void *reserved) noexcept -> jint;
		static auto get_java_class_name_for_TypeManager (jclass klass) noexcept -> char*;

	protected:
		static inline jmethodID Class_getName = nullptr;
		static inline JavaVM *jvm = nullptr;
	};
}

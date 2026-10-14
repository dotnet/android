#pragma once

#include <jni.h>

namespace xamarin::android {
	// Common interfaces shared between the CoreCLR and NativeAOT runtimes
	class HostCommon
	{
	public:
		static auto Java_JNI_OnLoad (JavaVM *vm, void *reserved) noexcept -> jint;
		static void init_logging_categories () noexcept;

	protected:
		static inline JavaVM *jvm = nullptr;
	};
}

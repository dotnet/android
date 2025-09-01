#pragma once

#include <jni.h>

namespace xamarin::android {
	class Host
	{
	public:
		static auto Java_JNI_OnLoad (JavaVM *vm, void *reserved) noexcept -> jint;

	private:
		static inline JavaVM *jvm = nullptr;
	};
}

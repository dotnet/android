#pragma once

#include <jni.h>

namespace xamarin::android {
	class RuntimeEnvironment
	{
	public:
		static auto get_jnienv () noexcept -> JNIEnv*;
	};
}

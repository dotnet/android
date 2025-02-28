#pragma once

#include <jni.h>

namespace xamarin::android {
	class HostUtil
	{
	public:
		static auto get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref) noexcept -> jclass;
	};
}

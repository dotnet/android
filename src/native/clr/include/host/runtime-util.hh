#pragma once

#include <string_view>

#include <jni.h>

namespace xamarin::android {
	class RuntimeUtil
	{
	public:
		static auto get_class_from_runtime_field (JNIEnv *env, jclass runtime, std::string_view const& name, bool make_gref) noexcept -> jclass;
	};
}

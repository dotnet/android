#pragma once

#include <jni.h>

#include "../shared/log_types.hh"

namespace xamarin::android {
	class Host
	{
	public:
		static auto Java_JNI_OnLoad (JavaVM *vm, void *reserved) noexcept -> jint;
	};
}

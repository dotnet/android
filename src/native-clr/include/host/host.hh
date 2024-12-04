#pragma once

#include <string_view>

#include <jni.h>

#include "../runtime-base/timing.hh"
#include "../shared/log_types.hh"

namespace xamarin::android {
	class Host
	{
	public:
		static auto Java_JNI_OnLoad (JavaVM *vm, void *reserved) noexcept -> jint;
		static void Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
			jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset, jobject loader,
			jobjectArray assembliesJava, jboolean isEmulator, jboolean haveSplitApks);

		static auto get_timing () -> Timing*
		{
			return _timing.get ();
		}

	private:
		static inline std::unique_ptr<Timing> _timing{};
	};
}

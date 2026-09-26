#pragma once

#include <jni.h>

#include <shared/cpp-util.hh>

namespace xamarin::android {
	class OSBridge
	{
	public:
		static void initialize_on_onload (JavaVM *vm) noexcept;
		static auto lref_to_gref (JNIEnv *env, jobject lref) noexcept -> jobject;

		static auto ensure_jnienv () noexcept -> JNIEnv*
		{
			JNIEnv *env = nullptr;
			jvm->GetEnv ((void**)&env, JNI_VERSION_1_6);
			if (env == nullptr) {
				JavaVMAttachArgs args;
				args.version = JNI_VERSION_1_6;
				args.name = nullptr;
				args.group = nullptr;
				jvm->AttachCurrentThread (&env, &args);
				abort_unless (env != nullptr, "Unable to get a valid pointer to JNIEnv");
			}

			return env;
		}

	private:
		static inline JavaVM *jvm = nullptr;
	};
}

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
			jvm->GetEnv (reinterpret_cast<void**> (&env), JNI_VERSION_1_6);
			if (env == nullptr) {
				JavaVMAttachArgs args;
				args.version = JNI_VERSION_1_6;
				args.name = nullptr;
				args.group = nullptr;
				jint status = jvm->AttachCurrentThread (&env, &args);
				abort_unless (status == JNI_OK && env != nullptr, "Unable to get a valid pointer to JNIEnv");
				register_thread_detach (env);
			}

			return env;
		}

	private:
		static void create_jni_env_key () noexcept;
		static void register_thread_detach (JNIEnv *env) noexcept;
		static void detach_current_thread (void *value) noexcept;

		static inline JavaVM *jvm = nullptr;
	};
}

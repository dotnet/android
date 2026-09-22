#pragma once

#include <cstdarg>
#include <cstdio>
#include <string_view>

#include <jni.h>

#include <managed-interface.hh>
#include <shared/cpp-util.hh>

#include "../runtime-base/logger.hh"

namespace xamarin::android {
	class OSBridge
	{
	public:
		static void initialize_on_onload (JavaVM *vm, JNIEnv *env) noexcept;
		static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;
		static auto lref_to_gref (JNIEnv *env, jobject lref) noexcept -> jobject;
		static auto get_object_ref_type (JNIEnv *env, void *handle) noexcept -> char;

		static void set_reference_logging_callbacks (reference_log_fn log_callback, reference_log_message_fn message_callback) noexcept;
		static void log_reference (
			ReferenceLogEvent kind,
			jobject current_handle,
			char current_type,
			jobject new_handle,
			char new_type,
			const char *thread_name,
			int thread_id,
			const char *stack_trace) noexcept;
		static void log_reference_message (const char *message) noexcept;
		static void log_reference_messagef (const char *format, ...) noexcept __attribute__ ((format (printf, 1, 2)));

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
		static inline jclass GCUserPeer_class = nullptr;
		static inline jmethodID GCUserPeer_ctor = nullptr;

		static inline reference_log_fn reference_log_callback = nullptr;
		static inline reference_log_message_fn reference_log_message_callback = nullptr;
	};
}

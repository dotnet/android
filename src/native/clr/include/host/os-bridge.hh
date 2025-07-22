#pragma once

#include <cstdio>
#include <pthread.h>

#include <jni.h>

#include "../runtime-base/logger.hh"

namespace xamarin::android {
	class OSBridge
	{
	public:
		static void initialize_on_onload (JavaVM *vm, JNIEnv *env) noexcept;
		static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;
		static auto lref_to_gref (JNIEnv *env, jobject lref) noexcept -> jobject;
		static auto get_object_ref_type (JNIEnv *env, void *handle) noexcept -> char;

		static auto get_gc_gref_count () noexcept -> int
		{
			return gc_gref_count;
		}

		static auto get_gc_weak_gref_count () noexcept -> int
		{
			return gc_weak_gref_count;
		}

		static void _monodroid_gref_log (const char *message) noexcept;
		static auto _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable) noexcept -> int;
		static void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable) noexcept;
		static void _monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable);
		static void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);

		static void _monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
		static void _monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);

		static auto ensure_jnienv () noexcept -> JNIEnv*
		{
			JNIEnv *env = nullptr;
			jvm->GetEnv ((void**)&env, JNI_VERSION_1_6);
			if (env == nullptr) {
				JavaVMAttachArgs args;
				args.version = JNI_VERSION_1_6;
				args.name = nullptr;
				args.group = nullptr;
				jvm->AttachCurrentThreadAsDaemon (&env, &args);
				abort_unless (env != nullptr, "Unable to get a valid pointer to JNIEnv");

				(void) pthread_once (&thread_local_env_init_key, make_key);
				pthread_setspecific (thread_local_env_key, env);
			}

			return env;
		}

	private:
		static inline pthread_key_t thread_local_env_key = {};
		static inline pthread_once_t thread_local_env_init_key = PTHREAD_ONCE_INIT;

		static void make_key () noexcept
		{
			pthread_key_create (&thread_local_env_key, &detach_thread_from_jni);
		}

		static void detach_thread_from_jni ([[maybe_unused]] void* unused) noexcept
		{
			jvm->DetachCurrentThread ();
		}

	private:
		static auto _monodroid_gref_inc () noexcept -> int;
		static auto _monodroid_gref_dec () noexcept -> int;
		static auto _get_stack_trace_line_end (char *m) noexcept -> char*;
		static void _write_stack_trace (FILE *to, char *from, LogCategories = LOG_NONE) noexcept;

	private:
		static inline JavaVM *jvm = nullptr;
		static inline jclass GCUserPeer_class = nullptr;
		static inline jmethodID GCUserPeer_ctor = nullptr;

		static inline int gc_gref_count = 0;
		static inline int gc_weak_gref_count = 0;
	};
}

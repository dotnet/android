#pragma once

#include <cstdio>

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
		static auto _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from) noexcept -> int;
		static void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from) noexcept;
		static void _monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from);
		static void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from);

		static void _monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from);
		static void _monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from);

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
		static auto _monodroid_gref_inc () noexcept -> int;
		static auto _monodroid_gref_dec () noexcept -> int;
		static void _write_stack_trace (FILE *to, const char *const from, LogCategories = LOG_NONE) noexcept;
		static void log_it (LogCategories category, std::string const& line, FILE *to, const char *const from, bool logcat_enabled) noexcept;

	private:
		static inline JavaVM *jvm = nullptr;
		static inline jclass GCUserPeer_class = nullptr;
		static inline jmethodID GCUserPeer_ctor = nullptr;

		static inline int gc_gref_count = 0;
		static inline int gc_weak_gref_count = 0;
	};
}

#pragma once

#include <cstdio>

#include <jni.h>

#include "../runtime-base/logger.hh"

namespace xamarin::android {
	class OSBridge
	{
	public:
		static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;
		static auto lref_to_gref (JNIEnv *env, jobject lref) noexcept -> jobject;

		static auto get_gc_gref_count () noexcept -> int
		{
			return gc_gref_count;
		}

		static auto get_gc_weak_gref_count () noexcept -> int
		{
			return gc_weak_gref_count;
		}

		static auto _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable) noexcept -> int;

	private:
		static auto _monodroid_gref_inc () noexcept -> int;
		static auto _monodroid_gref_dec () noexcept -> int;
		static auto _get_stack_trace_line_end (char *m) noexcept -> char*;
		static void _write_stack_trace (FILE *to, char *from, LogCategories = LOG_NONE) noexcept;

	private:
		static inline jclass GCUserPeer_class = nullptr;
		static inline jmethodID GCUserPeer_ctor = nullptr;

		static inline int gc_gref_count = 0;
		static inline int gc_weak_gref_count = 0;
	};
}

#pragma once

#include <jni.h>

namespace xamarin::android {
	class OSBridge
	{
	public:
		static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;
		static auto lref_to_gref (JNIEnv *env, jobject lref) noexcept -> jobject;

	private:
		static inline jclass GCUserPeer_class = nullptr;
		static inline jmethodID GCUserPeer_ctor = nullptr;
	};
}

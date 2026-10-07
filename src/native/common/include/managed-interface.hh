#pragma once

#include <cstdint>

#include <jni.h>
#include <runtime-base/binary-blob-loader.hh>

namespace xamarin::android {
	using jnienv_propagate_uncaught_exception_fn = void (*)(JNIEnv *env, jobject javaThread, jthrowable javaException);

	extern "C" {
	}

	// NOTE: Keep this in sync with managed side in src/Mono.Android/Android.Runtime/JNIEnvInit.cs
	struct JnienvInitializeArgs {
		JavaVM         *javaVm;
		JNIEnv         *env;
		jobject         grefLoader;
		unsigned int    logCategories;
		int             grefGcThreshold;
		jobject         grefIGCUserPeer;
		uint8_t         brokenExceptionTransitions;
		int             packageNamingPolicy;
		uint8_t         boundExceptionType;
		const BinaryBlobPayload *jniRemappingData;
		jobject         grefGCUserPeerable;
		jnienv_propagate_uncaught_exception_fn propagateUncaughtExceptionFn;
		int              maxGrefCount;
	};

	// Keep the enum values in sync with those in src/Mono.Android/AndroidRuntime/BoundExceptionType.cs
	enum class BoundExceptionType : uint8_t
	{
		System = 0x00,
		Java   = 0x01,
	};

	using jnienv_initialize_fn = void (*) (JnienvInitializeArgs*);
}

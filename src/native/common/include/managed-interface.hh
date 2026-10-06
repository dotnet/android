#pragma once

#include <cstdint>

#include <jni.h>

namespace xamarin::android {
	using jnienv_propagate_uncaught_exception_fn = void (*)(JNIEnv *env, jobject javaThread, jthrowable javaException);

	struct JniRemappingData {
		const void *type_replacements;
		const void *reverse_type_replacements;
		const void *method_replacement_index;
		const void *field_replacement_index;
		uint32_t    type_replacement_count;
		uint32_t    reverse_type_replacement_count;
		uint32_t    method_replacement_index_count;
		uint32_t    field_replacement_index_count;
	};

	extern "C" {
		[[gnu::visibility("default")]] extern const JniRemappingData jni_remapping_data;
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
		const JniRemappingData *jniRemappingData;
		jobject         grefGCUserPeerable;
		jnienv_propagate_uncaught_exception_fn propagateUncaughtExceptionFn;
		const char      *grefLogPath;
		const char      *lrefLogPath;
		const char      *referenceLogDirectory;
		uint8_t         lightGref;
		uint8_t         lightLref;
		uint8_t         grefToLogcat;
		uint8_t         lrefToLogcat;
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

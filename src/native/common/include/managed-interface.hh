#pragma once

#include <cstdint>

#include <jni.h>

namespace xamarin::android {
	// Values must be identical to those in src/Mono.Android/Android.Runtime/RuntimeNativeMethods.cs
	enum class TraceKind : uint32_t
	{
		Java    = 0x01,
		Managed = 0x02,
		Native  = 0x04,
		Signals = 0x08,
	};

	using jnienv_propagate_uncaught_exception_fn = void (*)(JNIEnv *env, jobject javaThread, jthrowable javaException);
	using jnienv_register_jni_natives_fn = void (*)(const jchar *typeName_ptr, int32_t typeName_len, jclass jniClass, const jchar *methods_ptr, int32_t methods_len);

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
		jmethodID       Loader_loadClass;
		jclass          grefClass;
		unsigned int    logCategories;
		int             version;
		int             grefGcThreshold;
		jobject         grefIGCUserPeer;
		uint8_t         brokenExceptionTransitions;
		int             packageNamingPolicy;
		uint8_t         boundExceptionType;
		int             jniAddNativeMethodRegistrationAttributePresent;
		const JniRemappingData *jniRemappingData;
		bool            marshalMethodsEnabled;
		jobject         grefGCUserPeerable;
		jnienv_propagate_uncaught_exception_fn propagateUncaughtExceptionFn;
		jnienv_register_jni_natives_fn registerJniNativesFn;
	};

	// Keep the enum values in sync with those in src/Mono.Android/AndroidRuntime/BoundExceptionType.cs
	enum class BoundExceptionType : uint8_t
	{
		System = 0x00,
		Java   = 0x01,
	};

	using jnienv_initialize_fn = void (*) (JnienvInitializeArgs*);
}

#include <host/host.hh>
#include <host/host-jni.hh>
#include <shared/log_types.hh>
#include <runtime-base/timing-internal.hh>

using namespace xamarin::android;

JNIEXPORT jint JNICALL
JNI_OnLoad (JavaVM *vm, void *reserved)
{
	return Host::Java_JNI_OnLoad (vm, reserved);
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_dumpTimingData ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jclass klass)
{
	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.dump ();
	}
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_register (JNIEnv *env, [[maybe_unused]] jclass klass, jstring managedType, jclass nativeClass, jstring methods)
{
	Host::Java_mono_android_Runtime_register (env, managedType, nativeClass, methods);
}

JNIEXPORT void JNICALL
Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
	jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset, jobject loader,
	jobjectArray assembliesJava, jboolean isEmulator,
	jboolean haveSplitApks)
{
	Host::Java_mono_android_Runtime_initInternal (
		env,
		klass,
		lang,
		runtimeApksJava,
		runtimeNativeLibDir,
		appDirs,
		localDateTimeOffset,
		loader,
		assembliesJava,
		isEmulator,
		haveSplitApks
	);
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_propagateUncaughtException (JNIEnv *env, [[maybe_unused]] jclass klass, jobject javaThread, jthrowable javaException)
{
	Host::propagate_uncaught_exception (env, javaThread, javaException);
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_notifyTimeZoneChanged ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jclass klass)
{
	// TODO: implement or remove
}

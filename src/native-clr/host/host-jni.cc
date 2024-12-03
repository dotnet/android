#include <host/host.hh>
#include <host/host-jni.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

JNIEXPORT jint JNICALL
JNI_OnLoad (JavaVM *vm, void *reserved)
{
	log_write (LOG_DEFAULT, LogLevel::Info, "JNI_OnLoad");

	return Host::Java_JNI_OnLoad (vm, reserved);
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_dumpTimingData ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jclass klass)
{
	// if (internal_timing == nullptr) {
	// 	return;
	// }

	// internal_timing->dump ();
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_register (JNIEnv *env, [[maybe_unused]] jclass klass, jstring managedType, jclass nativeClass, jstring methods)
{
}

JNIEXPORT void JNICALL
Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
	jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset, jobject loader,
	jobjectArray assembliesJava, jboolean isEmulator,
	jboolean haveSplitApks)
{
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_propagateUncaughtException (JNIEnv *env, [[maybe_unused]] jclass klass, jobject javaThread, jthrowable javaException)
{
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_notifyTimeZoneChanged ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jclass klass)
{
}

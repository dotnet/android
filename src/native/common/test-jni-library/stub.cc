#include <jni.h>

JNIEXPORT jint JNICALL
JNI_OnLoad ([[maybe_unused]] JavaVM *vm, [[maybe_unused]] void *reserved)
{
	// no-op, need just the JNI_OnLoad symbol to be present for JNI preload tests
	return JNI_VERSION_1_6;
}

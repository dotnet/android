#pragma once

#include <jni.h>

extern "C" {
	/*
	 * Class:     net_dot_android_Runtime
	 * Method:    initInternal
	 * Signature: (Ljava/lang/String;[Ljava/lang/String;[Ljava/lang/String;ILjava/lang/ClassLoader;ZZ)V
	 */
	JNIEXPORT void JNICALL Java_net_dot_android_Runtime_initInternal (JNIEnv *, jclass, jstring, jobjectArray, jobjectArray, jint, jobject, jboolean, jboolean);

	/*
	 * Class:     net_dot_android_Runtime
	 * Method:    propagateUncaughtException
	 * Signature: (Ljava/lang/Thread;Ljava/lang/Throwable;)V
	 */
	JNIEXPORT void JNICALL Java_net_dot_android_Runtime_propagateUncaughtException (JNIEnv *, jclass, jobject, jthrowable);

}

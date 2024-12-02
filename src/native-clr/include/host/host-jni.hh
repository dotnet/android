#pragma once

#include <jni.h>

extern "C" {
	/*
	 * Class:     mono_android_Runtime
	 * Method:    initInternal
	 * Signature: (Ljava/lang/String;[Ljava/lang/String;Ljava/lang/String;[Ljava/lang/String;ILjava/lang/ClassLoader;[Ljava/lang/String;IZZ)V
	 */
	JNIEXPORT void JNICALL Java_mono_android_Runtime_initInternal(JNIEnv *, jclass, jstring, jobjectArray, jstring, jobjectArray, jint, jobject, jobjectArray, jboolean, jboolean);

	/*
	 * Class:     mono_android_Runtime
	 * Method:    dumpTimingData
	 * Signature: ()V
	 */
	JNIEXPORT void JNICALL Java_mono_android_Runtime_dumpTimingData (JNIEnv *, jclass);

	/*
	 * Class:     mono_android_Runtime
	 * Method:    init
	 * Signature: (Ljava/lang/String;[Ljava/lang/String;Ljava/lang/String;[Ljava/lang/String;ILjava/lang/ClassLoader;[Ljava/lang/String;IZZ)V
	 */
	JNIEXPORT void JNICALL Java_mono_android_Runtime_init (JNIEnv *, jclass, jstring, jobjectArray, jstring, jobjectArray, jint, jobject, jobjectArray, jboolean, jboolean);

	/*
	 * Class:     mono_android_Runtime
	 * Method:    notifyTimeZoneChanged
	 * Signature: ()V
	 */
	JNIEXPORT void JNICALL Java_mono_android_Runtime_notifyTimeZoneChanged (JNIEnv *, jclass);

	/*
	 * Class:     mono_android_Runtime
	 * Method:    propagateUncaughtException
	 * Signature: (Ljava/lang/Thread;Ljava/lang/Throwable;)V
	 */
	JNIEXPORT void JNICALL Java_mono_android_Runtime_propagateUncaughtException (JNIEnv *, jclass, jobject, jthrowable);

	/*
	 * Class:     mono_android_Runtime
	 * Method:    register
	 * Signature: (Ljava/lang/String;Ljava/lang/Class;Ljava/lang/String;)V
	 */
	JNIEXPORT void JNICALL Java_mono_android_Runtime_register (JNIEnv *, jclass, jstring, jclass, jstring);

}

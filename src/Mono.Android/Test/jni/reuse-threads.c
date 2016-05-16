/*
 * https://bugzilla.xamarin.com/show_bug.cgi?id=13521
 * SIGSEGV while executing native code
 *
 * This appears to be similar to a (now private) forum thread:
 * http://forums.xamarin.com/discussion/3473/sigsegv-on-globaljava-lang-object-getobject
 *
 * What happens is that when using "certain" Android libraries (in this case,
 * the Sygic GPS navigation library: http://www.sygic.com/en ), the process
 * will abort with a SIGSEGV.
 *
 * What happens is:
 *  1. A thread 'T' is created. Java, pthread, doens't matter, as on Android
 *     java.lang.Thread is backed by pthreads anyway.
 *  2. Execution of T enters XA, JNIEnv.Handle is set via
 *     JNIInvokeInterface::AttachCurrentThread(), then the JNIEnv* is cached
 *     in TLS.
 *  3. T is disassociated with Dalvik "as if" via
 *     JNIInvokeInterface::DetachCurrentThread() and the JNIEnv* parameter T
 *     is invalidated.
 *  4. T calls JNIInvokeInterface::AttachCurrentThread(), and a new & different
 *     JNIEnv* value is created.
 *  5. T re-enters XA, and XA tries to use the (now invalid) JNIEnv.Handle value.
 *  6. *BOOM* [0]
 *
 * The actual Sygic library DOES NOT call DetachCurrentThread(). However, I
 * can get a similar crash by using it.
 *
 * Implementation:
 *
 * Steps 1 & 2 are easy: rt_invoke_callback_on_new_thread() uses
 * pthread_create() to launch _call_cb_from_new_thread() on a new Thread.
 * _call_cb_from_new_thread()'s thread is thread 'T'.
 *
 * Step 3 is harder, as the "trivial" idea of having the
 * worker thread do AttachCurrentThread(), DetachCurrentThread(),
 * AttachCurrentThread(), results in the second AttachCurrentThread()
 * returning the same JNIEnv* value as the first call.
 *
 * What is presumably happening is that the JNIEnv* pointer is held by a
 * Dalvik Thread*, which in turn is referenced by a java.lang.Thread instance.
 * We need to clear out all of those in order for AttachCurrentThread() to
 * return a new value, and we can do that by provoking a GC.
 *
 * Thus, Step 3 occurs in two parts:
 *
 * 3(a): Thread 'T' calls DetachCurrentThread(), then waits on a semaphore
 * while the main thread calls java.lang.Runtime.gc().
 * 3(b): The main thread calls Runtime.gc(), then posts to a semaphore so that
 * Thread 'T' continues execution.
 *
 * Once (3) is working, the rest fails as expected.
 *
 * [0] `adb logcat` output of the crash:
 * E/mono-rt ( 6999): Stacktrace:
 * E/mono-rt ( 6999):
 * E/mono-rt ( 6999):   at <unknown> <0xffffffff>
 * E/mono-rt ( 6999):   at (wrapper managed-to-native) Android.Runtime.JNIEnv._monodroid_get_identity_hash_code (intptr,intptr)
 * E/mono-rt ( 6999):   at Android.Runtime.JNIEnv.<Initialize>m__C2 (intptr)
 * E/mono-rt ( 6999):   at Java.Lang.Object.GetObject (intptr,Android.Runtime.JniHandleOwnership,System.Type)
 * E/mono-rt ( 6999):   at Java.Lang.Object._GetObject<T> (intptr,Android.Runtime.JniHandleOwnership)
 * E/mono-rt ( 6999):   at Java.Lang.Object.GetObject<T> (intptr,Android.Runtime.JniHandleOwnership)
 * E/mono-rt ( 6999):   at Xamarin.Android.RuntimeTests.JnienvTest.<CrossThreadObjectInteractions>m__8 (intptr,intptr) [0x0003b] in /Users/jon/Dropbox/Developer/xamarin/monodroid/tests/runtime/Java.Interop/JnienvTest.cs:40
 * E/mono-rt ( 6999):   at (wrapper native-to-managed) Xamarin.Android.RuntimeTests.JnienvTest.<CrossThreadObjectInteractions>m__8 (intptr,intptr)
 * E/mono-rt ( 6999):
 * E/mono-rt ( 6999): =================================================================
 * E/mono-rt ( 6999): Got a SIGSEGV while executing native code. This usually indicates
 * E/mono-rt ( 6999): a fatal error in the mono runtime or one of the native libraries
 * E/mono-rt ( 6999): used by your application.
 * E/mono-rt ( 6999): =================================================================
 * E/mono-rt ( 6999):
 *
 */
#include <assert.h>
#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <pthread.h>
#include <semaphore.h>
#include <android/log.h>
#include <jni.h>

typedef void (*CB)(JNIEnv *env, jobject self);

static JavaVM *gvm;
static sem_t start_gc_on_main;
static sem_t finished_gc_on_main;

JNIEXPORT jint JNICALL
JNI_OnLoad (JavaVM *vm, void *reserved)
{
	int r;
	if ((r = sem_init (&start_gc_on_main, 0, 0)) < 0 ||
			(r = sem_init (&finished_gc_on_main, 0, 0)) < 0) {
		__android_log_print (ANDROID_LOG_FATAL, "XA/RuntimeTest", "Could not allocate semaphore: %i %s", errno, strerror (errno));
		exit (-1);
	}
	gvm = vm;
	return JNI_VERSION_1_6;
}

static JNIEnv *
_get_env (const char *where)
{
	JNIEnv *env;
	int r = (*gvm)->AttachCurrentThread (gvm, &env, NULL);
	if (r != JNI_OK) {
		__android_log_print (ANDROID_LOG_FATAL, "XA/RuntimeTest", "AttachCurrentThread() failed at %s: %i", where, r);
		exit (-1);
	}
	return env;
}

static jobject
_create_java_instance (JNIEnv *env)
{
	jclass    Object_class  = (*env)->FindClass (env, "java/lang/Object");
	jmethodID Object_ctor   = (*env)->GetMethodID (env, Object_class, "<init>", "()V");

	jobject   instance      = (*env)->NewObject (env, Object_class, Object_ctor);

	(*env)->DeleteLocalRef (env, Object_class);

	return instance;
}

static void*
_call_cb_from_new_thread (void *cb)
{
	JNIEnv *env, *old_env;
	int r;
	CB _cb = cb;

	old_env = env = _get_env ("_call_cb_from_new_thread");

	/* 2: Execution of T enters managed code... */
	_cb (env, NULL);

	/* 3(a): Detach current thread from JVM... */
	r = (*gvm)->DetachCurrentThread (gvm);
	assert (r == 0 && !"DetachCurrentThread() failed!");
	env = NULL;

	r = sem_post (&start_gc_on_main);
	assert (r == 0 && !"sem_post(start_gc_on_main)");
	r = sem_wait (&finished_gc_on_main);
	assert (r == 0 && !"sem_wait(finished_gc_on_main)");

	/* 4: T calls AttachCurrentThread(), JNIEnv* differs */
	env = _get_env ("_call_cb_from_new_thread: take 2!");
	if (old_env == env) {
		__android_log_print (ANDROID_LOG_INFO, "XA/RuntimeTest", "FAILURE: JNIEnv* wasn't changed!");
	}

	/* 5: Execution of T enters managed code... */
	jobject instance = _create_java_instance (env);
	_cb (env, instance);

	return NULL;
}

static void
_gc (JNIEnv* env)
{
	jclass    Runtime_class       = (*env)->FindClass (env, "java/lang/Runtime");
	jmethodID Runtime_getRuntime  = (*env)->GetStaticMethodID (env, Runtime_class, "getRuntime", "()Ljava/lang/Runtime;");
	jmethodID Runtime_gc          = (*env)->GetMethodID (env, Runtime_class, "gc", "()V");
	jobject   runtime             = (*env)->CallStaticObjectMethod (env, Runtime_class, Runtime_getRuntime);
	(*env)->CallVoidMethod (env, runtime, Runtime_gc);
	(*env)->DeleteLocalRef (env, Runtime_class);
	(*env)->DeleteLocalRef (env, runtime);
}

JNIEXPORT int JNICALL
rt_invoke_callback_on_new_thread (CB cb)
{
	pthread_t t;
	int r;
	void *tr;
	JNIEnv *env = _get_env ("rt_invoke_callback_on_new_thread");

	/* 1: Craete a thread... */
	r = pthread_create (&t, NULL, _call_cb_from_new_thread, cb);
	if (r) {
		__android_log_print (ANDROID_LOG_INFO, "XA/RuntimeTest", "InvokeFromNewThread: pthread_create() failed! %i: %s", r, strerror (r));
		return -1;
	}

	/* 3(b): Ensure Dalvik gets a chance to cleanup the old JNIEnv* */
	sem_wait (&start_gc_on_main);
	_gc (env);
	_gc (env);  /* for good measure... */

	/* Allow (4) to execute... */
	sem_post (&finished_gc_on_main);

	pthread_join (t, &tr);

	return 0;
}


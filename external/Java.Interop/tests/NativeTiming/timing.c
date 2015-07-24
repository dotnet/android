#include <stdio.h>
#include <sys/time.h>
#include <jni.h>

#ifdef PLATFORM_ANDROID
#include <android/log.h>
#endif  /* def PLATFORM_ANDROID */

static void
_log (const char *format, ...)
{
	va_list args;
	va_start (args, format);
#ifdef PLATFORM_ANDROID
	__android_log_vprint (ANDROID_LOG_INFO, "*Java.Interop*", format, args);
#else   /* def PLATFORM_ANDROID */
	vprintf (format, args);
#endif  /* !def PLATFORM_ANDROID */
	va_end (args);
}

void
foo_void_timing (void)
{
}

int
foo_int_timing (void)
{
	return 0;
}

void*
foo_ptr_timing (void)
{
	return 0;
}

void
foo_void_a1_timing (void *obj1)
{
}

void
foo_void_a2_timing (void *obj1, void *obj2)
{
}

void
foo_void_a3_timing (void *obj1, void *obj2, void *obj3)
{
}

void
foo_void_ai1_timing (int i1)
{
}

void
foo_void_ai2_timing (int i1, int i2)
{
}

void
foo_void_ai3_timing (int i1, int i2, int i3)
{
}

struct FooMethods {
	void  (*instance_void)(void);
	int   (*instance_int)(void);
	void* (*instance_ptr)(void);

	void  (*void_1_args)(void *);
	void  (*void_2_args)(void *, void *);
	void  (*void_3_args)(void *, void *, void *);

	void  (*void_1_iargs)(int);
	void  (*void_2_iargs)(int, int);
	void  (*void_3_iargs)(int, int, int);
};

void
foo_get_methods (struct FooMethods* methods)
{
	methods->instance_void = foo_void_timing;
	methods->instance_int  = foo_int_timing;
	methods->instance_ptr  = foo_ptr_timing;

	methods->void_1_args   = foo_void_a1_timing;
	methods->void_2_args   = foo_void_a2_timing;
	methods->void_3_args   = foo_void_a3_timing;

	methods->void_1_iargs   = foo_void_ai1_timing;
	methods->void_2_iargs   = foo_void_ai2_timing;
	methods->void_3_iargs   = foo_void_ai3_timing;
}

static jmethodID Timing_StaticVoidMethod;
static jmethodID Timing_StaticIntMethod;
static jmethodID Timing_StaticObjectMethod;

static jmethodID Timing_VirtualVoidMethod;
static jmethodID Timing_VirtualIntMethod;
static jmethodID Timing_VirtualObjectMethod;

static jmethodID Timing_FinalVoidMethod;
static jmethodID Timing_FinalIntMethod;
static jmethodID Timing_FinalObjectMethod;

static jmethodID Timing_StaticVoidMethod1Args;
static jmethodID Timing_StaticVoidMethod2Args;
static jmethodID Timing_StaticVoidMethod3Args;

static jmethodID Timing_StaticVoidMethod1IArgs;
static jmethodID Timing_StaticVoidMethod2IArgs;
static jmethodID Timing_StaticVoidMethod3IArgs;

static jclass       Object_class;
static jmethodID    Object_init;

#if 0
static void    (*CallStaticVoidMethod)(JNIEnv*, jclass, jmethodID, ...);
static int     (*CallStaticIntMethod)(JNIEnv*, jclass, jmethodID, ...);
static jobject (*CallStaticObjectMethod)(JNIEnv*, jclass, jmethodID, ...);

static void    (*CallVoidMethod)(JNIEnv*, jobject, jmethodID, ...);
static int     (*CallIntMethod)(JNIEnv*, jobject, jmethodID, ...);
static jobject (*CallObjectMethod)(JNIEnv*, jobject, jmethodID, ...);

static void    (*CallNonvirtualVoidMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
static int     (*CallNonvirtualIntMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
static jobject (*CallNonvirtualObjectMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
#endif

void
foo_init (JNIEnv *env)
{
	jclass Timing, Object;

	_log ("# NativeTiming: foo_init; env=%p\n", env);

	/* libbar.so loading test */
	if (!env)
		return;

	Timing = (*env)->FindClass (env, "com/xamarin/interop/performance/JavaTiming");
	if (!Timing)
		return;

	Object = (*env)->FindClass (env, "java/lang/Object");
	if (!Object)
		return;
	Object_class = (*env)->NewGlobalRef (env, Object);
	(*env)->DeleteLocalRef (env, Object);

	Object_init = (*env)->GetMethodID (env, Object_class, "<init>", "()V");

	#if 0
	CallStaticVoidMethod    = (*env)->CallStaticVoidMethod;
	CallStaticIntMethod     = (*env)->CallStaticIntMethod;
	CallStaticObjectMethod  = (*env)->CallStaticObjectMethod;

	CallVoidMethod    = (*env)->CallVoidMethod;
	CallIntMethod     = (*env)->CallIntMethod;
	CallObjectMethod  = (*env)->CallObjectMethod;

	CallNonvirtualVoidMethod    = (*env)->CallNonvirtualVoidMethod;
	CallNonvirtualIntMethod     = (*env)->CallNonvirtualIntMethod;
	CallNonvirtualObjectMethod  = (*env)->CallNonvirtualObjectMethod;
	#endif

	Timing_StaticVoidMethod = (*env)->GetStaticMethodID (env, Timing,
			"StaticVoidMethod", "()V");
	Timing_StaticIntMethod = (*env)->GetStaticMethodID (env, Timing,
			"StaticIntMethod", "()I");
	Timing_StaticObjectMethod = (*env)->GetStaticMethodID (env, Timing,
			"StaticObjectMethod", "()Ljava/lang/Object;");

	Timing_VirtualVoidMethod = (*env)->GetMethodID (env, Timing,
			"VirtualVoidMethod", "()V");
	Timing_VirtualIntMethod = (*env)->GetMethodID (env, Timing,
			"VirtualIntMethod", "()I");
	Timing_VirtualObjectMethod = (*env)->GetMethodID (env, Timing,
			"VirtualObjectMethod", "()Ljava/lang/Object;");

	Timing_FinalVoidMethod = (*env)->GetMethodID (env, Timing,
			"FinalVoidMethod", "()V");
	Timing_FinalIntMethod = (*env)->GetMethodID (env, Timing,
			"FinalIntMethod", "()I");
	Timing_FinalObjectMethod = (*env)->GetMethodID (env, Timing,
			"FinalObjectMethod", "()Ljava/lang/Object;");

	Timing_StaticVoidMethod1Args = (*env)->GetStaticMethodID (env, Timing,
			"StaticVoidMethod1Args", "(Ljava/lang/Object;)V");
	Timing_StaticVoidMethod2Args = (*env)->GetStaticMethodID (env, Timing,
			"StaticVoidMethod2Args", "(Ljava/lang/Object;Ljava/lang/Object;)V");
	Timing_StaticVoidMethod3Args = (*env)->GetStaticMethodID (env, Timing,
			"StaticVoidMethod3Args", "(Ljava/lang/Object;Ljava/lang/Object;Ljava/lang/Object;)V");

	Timing_StaticVoidMethod1IArgs = (*env)->GetStaticMethodID (env, Timing,
			"StaticVoidMethod1IArgs", "(I)V");
	Timing_StaticVoidMethod2IArgs = (*env)->GetStaticMethodID (env, Timing,
			"StaticVoidMethod2IArgs", "(II)V");
	Timing_StaticVoidMethod3IArgs = (*env)->GetStaticMethodID (env, Timing,
			"StaticVoidMethod3IArgs", "(III)V");

	(*env)->DeleteLocalRef (env, Timing);
}

static long long
current_time_millis (void)
{
	struct timeval tv;

	gettimeofday(&tv, (struct timezone *) NULL);
	long long when = tv.tv_sec * 1000LL + tv.tv_usec / 1000;
	return when;
}

void
foo_get_native_jni_timings (JNIEnv *env, int count, jclass klass, jobject self, long long *jniTimes)
{
	int i;
	long long start, end;

	jobject obj1 = (*env)->NewObject(env, Object_class, Object_init),
		obj2 = (*env)->NewObject(env, Object_class, Object_init),
		obj3 = (*env)->NewObject(env, Object_class, Object_init);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticVoidMethod (env, klass, Timing_StaticVoidMethod);
	end = current_time_millis ();

	jniTimes [0] = end - start;
	_log ("# NativeTiming: foo/timing: static void    method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [0], jniTimes [0] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticIntMethod (env, klass, Timing_StaticIntMethod);
	end = current_time_millis ();

	jniTimes [1] = end - start;
	_log ("# NativeTiming: foo/timing: static int     method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [1], jniTimes [1] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticObjectMethod (env, klass, Timing_StaticObjectMethod);
	end = current_time_millis ();

	jniTimes [2] = end - start;
	_log ("# NativeTiming: foo/timing: static Object  method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [2], jniTimes [2] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallVoidMethod (env, self, Timing_VirtualVoidMethod);
	end = current_time_millis ();

	jniTimes [3] = end - start;
	_log ("# NativeTiming: foo/timing: virtual void   method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [3], jniTimes [3] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallIntMethod (env, self, Timing_VirtualIntMethod);
	end = current_time_millis ();

	jniTimes [4] = end - start;
	_log ("# NativeTiming: foo/timing: virtual int    method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [4], jniTimes [4] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallObjectMethod (env, self, Timing_VirtualObjectMethod);
	end = current_time_millis ();

	jniTimes [5] = end - start;
	_log ("# NativeTiming: foo/timing: virtual Object method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [5], jniTimes [5] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallNonvirtualVoidMethod (env, self, klass, Timing_FinalVoidMethod);
	end = current_time_millis ();

	jniTimes [6] = end - start;
	_log ("# NativeTiming: foo/timing: final void     method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [6], jniTimes [6] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallNonvirtualIntMethod (env, self, klass, Timing_FinalIntMethod);
	end = current_time_millis ();

	jniTimes [7] = end - start;
	_log ("# NativeTiming: foo/timing: final int      method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [7], jniTimes [7] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallNonvirtualObjectMethod (env, self, klass, Timing_FinalObjectMethod);
	end = current_time_millis ();

	jniTimes [8] = end - start;
	_log ("# NativeTiming: foo/timing: final Object   method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [8], jniTimes [8] / (double) count);



	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticVoidMethod (env, klass, Timing_StaticVoidMethod1Args, obj1);
	end = current_time_millis ();

	jniTimes [9] = end - start;
	_log ("# NativeTiming: foo/timing: static void o1 method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [9], jniTimes [9] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticVoidMethod (env, klass, Timing_StaticVoidMethod2Args, obj1, obj2);
	end = current_time_millis ();

	jniTimes [10] = end - start;
	_log ("# NativeTiming: foo/timing: static void o2 method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [10], jniTimes [10] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticVoidMethod (env, klass, Timing_StaticVoidMethod3Args, obj1, obj2, obj3);
	end = current_time_millis ();

	jniTimes [11] = end - start;
	_log ("# NativeTiming: foo/timing: static void o3 method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [11], jniTimes [11] / (double) count);



	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticVoidMethod (env, klass, Timing_StaticVoidMethod1IArgs, 42);
	end = current_time_millis ();

	jniTimes [12] = end - start;
	_log ("# NativeTiming: foo/timing: static void i1 method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [12], jniTimes [12] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticVoidMethod (env, klass, Timing_StaticVoidMethod2IArgs, 42, 42);
	end = current_time_millis ();

	jniTimes [13] = end - start;
	_log ("# NativeTiming: foo/timing: static void i2 method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [13], jniTimes [13] / (double) count);

	start = current_time_millis ();
	for (i = 0; i < count; i++)
		(*env)->CallStaticVoidMethod (env, klass, Timing_StaticVoidMethod3IArgs, 42, 42, 42);
	end = current_time_millis ();

	jniTimes [14] = end - start;
	_log ("# NativeTiming: foo/timing: static void i3 method invoke: %10lli ms | average: %10f ms\n",
			jniTimes [14], jniTimes [14] / (double) count);
}


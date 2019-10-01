#ifdef WINDOWS
#include <windef.h>
#include <winbase.h>
#include <shlobj.h>
#include <objbase.h>
#include <knownfolders.h>
#include <shlwapi.h>
#endif

#include "globals.hh"

using namespace xamarin::android::internal;

/* Invoked by System.Core.dll!System.IO.MemoryMappedFiles.MemoryMapImpl.getpagesize */
MONO_API int
monodroid_getpagesize (void)
{
#ifndef WINDOWS
	return getpagesize ();
#else
	SYSTEM_INFO info;
	GetSystemInfo (&info);
	return info.dwPageSize;
#endif
}

/* Invoked by:
   - System.Core.dll!System.TimeZoneInfo.Android.GetDefaultTimeZoneName
   - Mono.Android.dll!Android.Runtime.AndroidEnvironment.GetDefaultTimeZone
*/

MONO_API void
monodroid_free (void *ptr)
{
	free (ptr);
}

MONO_API int
monodroid_get_system_property (const char *name, char **value)
{
	return androidSystem.monodroid_get_system_property (name, value);
}

MONO_API int
_monodroid_max_gref_get (void)
{
	return static_cast<int>(androidSystem.get_max_gref_count ());
}

MONO_API int
_monodroid_gref_get (void)
{
	return osBridge.get_gc_gref_count ();
}

MONO_API void
_monodroid_gref_log (const char *message)
{
	osBridge._monodroid_gref_log (message);
}

MONO_API int
_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
	return osBridge._monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_gref_log_delete (handle, type, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_weak_gref_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_weak_gref_delete (handle, type, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_lref_log_new (lrefc, handle, type, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_lref_log_delete (lrefc, handle, type, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_gc_wait_for_bridge_processing (void)
{
	mono_gc_wait_for_bridge_processing ();
}

/* !DO NOT REMOVE! Used by Mono BCL */
MONO_API int
_monodroid_get_android_api_level (void)
{
	return monodroidRuntime.get_android_api_level ();
}

/* Can be called by a native debugger to break the wait on startup */
MONO_API void
monodroid_clear_gdb_wait (void)
{
	monodroidRuntime.set_monodroid_gdb_wait (false);
}

MONO_API void*
_monodroid_get_identity_hash_code (JNIEnv *env, void *v)
{
	intptr_t rv = env->CallStaticIntMethod (monodroidRuntime.get_java_class_System (), monodroidRuntime.get_java_class_method_System_identityHashCode (), v);
	return (void*) rv;
}

MONO_API void*
_monodroid_timezone_get_default_id (void)
{
	JNIEnv *env          = osBridge.ensure_jnienv ();
	jmethodID getDefault = env->GetStaticMethodID (monodroidRuntime.get_java_class_TimeZone (), "getDefault", "()Ljava/util/TimeZone;");
	jmethodID getID      = env->GetMethodID (monodroidRuntime.get_java_class_TimeZone (), "getID",      "()Ljava/lang/String;");
	jobject d            = env->CallStaticObjectMethod (monodroidRuntime.get_java_class_TimeZone (), getDefault);
	jstring id           = reinterpret_cast<jstring> (env->CallObjectMethod (d, getID));
	const char *mutf8    = env->GetStringUTFChars (id, nullptr);
	char *def_id         = strdup (mutf8);

	env->ReleaseStringUTFChars (id, mutf8);
	env->DeleteLocalRef (id);
	env->DeleteLocalRef (d);

	return def_id;
}

MONO_API void
_monodroid_counters_dump (const char *format, ...)
{
	va_list args;
	va_start (args, format);
	monodroidRuntime.dump_counters_v (format, args);
	va_end (args);
}

/* !DO NOT REMOVE! Used by libgdiplus.so */
MONO_API int
_monodroid_get_display_dpi (float *x_dpi, float *y_dpi)
{
	return monodroidRuntime.get_display_dpi (x_dpi, y_dpi);
}

MONO_API const char *
monodroid_typemap_java_to_managed (const char *java)
{
	return embeddedAssemblies.typemap_java_to_managed (java);
}

MONO_API const char *
monodroid_typemap_managed_to_java (const char *managed)
{
	return embeddedAssemblies.typemap_managed_to_java (managed);
}

MONO_API int monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix)
{
	embeddedAssemblies.set_assemblies_prefix (prefix);
	return 0;
}

extern "C" void* monodroid_dylib_mono_new (const char *libmono_path)
{
	return nullptr;
}

extern "C" void monodroid_dylib_mono_free (void *mono_imports)
{
	// no-op
}

/*
  this function is used from JavaInterop and should be treated as public API
  https://github.com/xamarin/java.interop/blob/master/src/java-interop/java-interop-gc-bridge-mono.c#L266

  it should also accept libmono_path = nullptr parameter
*/
extern "C" int monodroid_dylib_mono_init (void *mono_imports, const char *libmono_path)
{
	if (mono_imports == nullptr)
		return FALSE;
	return TRUE;
}

extern "C" void*  monodroid_get_dylib (void)
{
	return nullptr;
}

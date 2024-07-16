#include "android-system.hh"
#include "globals.hh"
#include "internal-pinvokes.hh"
#include "jni-remapping.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

unsigned int
monodroid_get_log_categories ()
{
	return log_categories;
}

int
monodroid_get_system_property (const char *name, char **value)
{
<<<<<<< HEAD
<<<<<<< HEAD
    return AndroidSystem::monodroid_get_system_property (name, value);
=======
        return AndroidSystem::monodroid_get_system_property (name, value);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    return AndroidSystem::monodroid_get_system_property (name, value);
>>>>>>> 87926b42b (Formatting)
}

int
monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix)
{
<<<<<<< HEAD
<<<<<<< HEAD
    embeddedAssemblies.set_assemblies_prefix (prefix);
    return 0;
=======
        embeddedAssemblies.set_assemblies_prefix (prefix);
        return 0;
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    embeddedAssemblies.set_assemblies_prefix (prefix);
    return 0;
>>>>>>> 87926b42b (Formatting)
}

void
monodroid_log (LogLevel level, LogCategories category, const char *message)
{
	switch (level) {
		case LogLevel::Verbose:
		case LogLevel::Debug:
			log_debug_nocheck (category, message);
			break;

		case LogLevel::Info:
			log_info_nocheck (category, message);
			break;

		case LogLevel::Warn:
		case LogLevel::Silent: // warn is always printed
			log_warn (category, message);
			break;

		case LogLevel::Error:
			log_error (category, message);
			break;

		case LogLevel::Fatal:
			log_fatal (category, message);
			break;

		default:
		case LogLevel::Unknown:
		case LogLevel::Default:
			log_info_nocheck (category, message);
			break;
	}
}

void
monodroid_free (void *ptr)
{
<<<<<<< HEAD
<<<<<<< HEAD
    free (ptr);
=======
        free (ptr);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    free (ptr);
>>>>>>> 87926b42b (Formatting)
}

int
_monodroid_max_gref_get ()
{
<<<<<<< HEAD
<<<<<<< HEAD
    return static_cast<int>(AndroidSystem::get_max_gref_count ());
=======
        return static_cast<int>(AndroidSystem::get_max_gref_count ());
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    return static_cast<int>(AndroidSystem::get_max_gref_count ());
>>>>>>> 87926b42b (Formatting)
}

int
_monodroid_gref_get ()
{
	return osBridge.get_gc_gref_count ();
}


void
_monodroid_gref_log (const char *message)
{
<<<<<<< HEAD
<<<<<<< HEAD
    osBridge._monodroid_gref_log (message);
=======
        osBridge._monodroid_gref_log (message);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    osBridge._monodroid_gref_log (message);
>>>>>>> 87926b42b (Formatting)
}

int
_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
<<<<<<< HEAD
<<<<<<< HEAD
    return osBridge._monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
=======
        return osBridge._monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    return osBridge._monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
>>>>>>> 87926b42b (Formatting)
}

void
_monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
<<<<<<< HEAD
<<<<<<< HEAD
    osBridge._monodroid_gref_log_delete (handle, type, threadName, threadId, from, from_writable);
=======
        osBridge._monodroid_gref_log_delete (handle, type, threadName, threadId, from, from_writable);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    osBridge._monodroid_gref_log_delete (handle, type, threadName, threadId, from, from_writable);
>>>>>>> 87926b42b (Formatting)
}

int
_monodroid_weak_gref_get ()
{
	return osBridge.get_gc_weak_gref_count ();
}

void
_monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
<<<<<<< HEAD
<<<<<<< HEAD
    osBridge._monodroid_weak_gref_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
=======
        osBridge._monodroid_weak_gref_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    osBridge._monodroid_weak_gref_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
>>>>>>> 87926b42b (Formatting)
}

void
_monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
<<<<<<< HEAD
<<<<<<< HEAD
    osBridge._monodroid_weak_gref_delete (handle, type, threadName, threadId, from, from_writable);
=======
        osBridge._monodroid_weak_gref_delete (handle, type, threadName, threadId, from, from_writable);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    osBridge._monodroid_weak_gref_delete (handle, type, threadName, threadId, from, from_writable);
>>>>>>> 87926b42b (Formatting)
}

void
_monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
<<<<<<< HEAD
<<<<<<< HEAD
    osBridge._monodroid_lref_log_new (lrefc, handle, type, threadName, threadId, from, from_writable);
=======
        osBridge._monodroid_lref_log_new (lrefc, handle, type, threadName, threadId, from, from_writable);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    osBridge._monodroid_lref_log_new (lrefc, handle, type, threadName, threadId, from, from_writable);
>>>>>>> 87926b42b (Formatting)
}

void
_monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
<<<<<<< HEAD
<<<<<<< HEAD
    osBridge._monodroid_lref_log_delete (lrefc, handle, type, threadName, threadId, from, from_writable);
=======
        osBridge._monodroid_lref_log_delete (lrefc, handle, type, threadName, threadId, from, from_writable);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    osBridge._monodroid_lref_log_delete (lrefc, handle, type, threadName, threadId, from, from_writable);
>>>>>>> 87926b42b (Formatting)
}

void
_monodroid_gc_wait_for_bridge_processing ()
{
<<<<<<< HEAD
<<<<<<< HEAD
    mono_gc_wait_for_bridge_processing ();
=======
        mono_gc_wait_for_bridge_processing ();
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    mono_gc_wait_for_bridge_processing ();
>>>>>>> 87926b42b (Formatting)
}

<<<<<<< HEAD
=======
int
_monodroid_get_android_api_level ()
{
<<<<<<< HEAD
<<<<<<< HEAD
    return monodroidRuntime.get_android_api_level ();
=======
        return monodroidRuntime.get_android_api_level ();
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    return monodroidRuntime.get_android_api_level ();
>>>>>>> 87926b42b (Formatting)
}

>>>>>>> badc53112 (Let's see what breaks)
void
monodroid_clear_gdb_wait ()
{
<<<<<<< HEAD
<<<<<<< HEAD
    monodroidRuntime.set_monodroid_gdb_wait (false);
=======
        monodroidRuntime.set_monodroid_gdb_wait (false);
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    monodroidRuntime.set_monodroid_gdb_wait (false);
>>>>>>> 87926b42b (Formatting)
}

void*
_monodroid_get_identity_hash_code (JNIEnv *env, void *v)
{
<<<<<<< HEAD
<<<<<<< HEAD
    intptr_t rv = env->CallStaticIntMethod (monodroidRuntime.get_java_class_System (), monodroidRuntime.get_java_class_method_System_identityHashCode (), v);
    return (void*) rv;
=======
        intptr_t rv = env->CallStaticIntMethod (monodroidRuntime.get_java_class_System (), monodroidRuntime.get_java_class_method_System_identityHashCode (), v);
        return (void*) rv;
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
    intptr_t rv = env->CallStaticIntMethod (monodroidRuntime.get_java_class_System (), monodroidRuntime.get_java_class_method_System_identityHashCode (), v);
    return (void*) rv;
>>>>>>> 87926b42b (Formatting)
}

void*
_monodroid_timezone_get_default_id ()
{
	JNIEnv *env          = osBridge.ensure_jnienv ();
	jmethodID getDefault = env->GetStaticMethodID (monodroidRuntime.get_java_class_TimeZone (), "getDefault", "()Ljava/util/TimeZone;");
	jmethodID getID      = env->GetMethodID (monodroidRuntime.get_java_class_TimeZone (), "getID",      "()Ljava/lang/String;");
	jobject d            = env->CallStaticObjectMethod (monodroidRuntime.get_java_class_TimeZone (), getDefault);
	jstring id           = reinterpret_cast<jstring> (env->CallObjectMethod (d, getID));
	const char *mutf8    = env->GetStringUTFChars (id, nullptr);

	if (mutf8 == nullptr) {
		log_error (LOG_DEFAULT, "Failed to convert Java TimeZone ID to UTF8 (out of memory?)");
		env->DeleteLocalRef (id);
		env->DeleteLocalRef (d);
		return nullptr;
	}

	char *def_id         = strdup (mutf8);

	env->ReleaseStringUTFChars (id, mutf8);
	env->DeleteLocalRef (id);
	env->DeleteLocalRef (d);

	return def_id;
}

void
_monodroid_counters_dump ([[maybe_unused]] const char *format, [[maybe_unused]] va_list args)
{
#if !defined (NET)
	monodroidRuntime.dump_counters_v (format, args);
#endif // ndef NET
}

managed_timing_sequence*
monodroid_timing_start (const char *message)
{
	if (timing == nullptr)
		return nullptr;

	managed_timing_sequence *ret = timing->get_available_sequence ();
	if (message != nullptr) {
		log_write (LOG_TIMING, LogLevel::Info, message);
	}
	ret->period.mark_start ();

	return ret;
}

void
monodroid_timing_stop (managed_timing_sequence *sequence, const char *message)
{
	static constexpr const char DEFAULT_MESSAGE[] = "Managed Timing";

	if (sequence == nullptr)
		return;

	sequence->period.mark_end ();
	Timing::info (sequence->period, message == nullptr ? DEFAULT_MESSAGE : message);
	timing->release_sequence (sequence);
}

char**
monodroid_strsplit (const char *str, const char *delimiter, size_t max_tokens)
{
	return Util::monodroid_strsplit (str, delimiter, max_tokens);
}

void
monodroid_strfreev (char **str_array)
{
	Util::monodroid_strfreev (str_array);
}

char*
monodroid_strdup_printf (const char *format, ...)
{
	va_list args;

	va_start (args, format);
	char *ret = Util::monodroid_strdup_vprintf (format, args);
	va_end (args);

	return ret;
}

char*
monodroid_TypeManager_get_java_class_name (jclass klass)
{
	return monodroidRuntime.get_java_class_name_for_TypeManager (klass);
}

int
monodroid_get_namespaced_system_property (const char *name, char **value)
{
	return static_cast<int>(AndroidSystem::monodroid_get_system_property (name, value));
}

FILE*
monodroid_fopen (const char* filename, const char* mode)
{
	return Util::monodroid_fopen (filename, mode);
}

int
send_uninterrupted (int fd, void *buf, int len)
{
	if (len < 0)
		len = 0;
	return Util::send_uninterrupted (fd, buf, static_cast<size_t>(len));
}

int
recv_uninterrupted (int fd, void *buf, int len)
{
	if (len < 0)
		len = 0;
	return static_cast<int>(Util::recv_uninterrupted (fd, buf, static_cast<size_t>(len)));
}

void
set_world_accessable (const char *path)
{
	Util::set_world_accessable (path);
}

void
create_public_directory (const char *dir)
{
	Util::create_public_directory (dir);
}

char*
path_combine (const char *path1, const char *path2)
{
	return Util::path_combine (path1, path2);
}

void*
monodroid_dylib_mono_new ([[maybe_unused]] const char *libmono_path)
{
	return nullptr;
}

void
monodroid_dylib_mono_free ([[maybe_unused]] void *mono_imports)
{
	// no-op
}

/*
  this function is used from JavaInterop and should be treated as public API
  https://github.com/dotnet/java-interop/blob/master/src/java-interop/java-interop-gc-bridge-mono.c#L266

  it should also accept libmono_path = nullptr parameter
<<<<<<< HEAD
<<<<<<< HEAD
 */
=======
*/
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
 */
>>>>>>> 87926b42b (Formatting)
int
monodroid_dylib_mono_init (void *mono_imports, [[maybe_unused]] const char *libmono_path)
{
	if (mono_imports == nullptr)
		return FALSE;
	return TRUE;
}

void*
monodroid_get_dylib ()
{
	return nullptr;
}

const char*
_monodroid_lookup_replacement_type (const char *jniSimpleReference)
{
	return JniRemapping::lookup_replacement_type (jniSimpleReference);
}

const JniRemappingReplacementMethod*
_monodroid_lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature)
{
	return JniRemapping::lookup_replacement_method_info (jniSourceType, jniMethodName, jniMethodSignature);
}

void
monodroid_log_traces (uint32_t kind, const char *first_line)
{
	JNIEnv *env = osBridge.ensure_jnienv ();
	auto tk = static_cast<TraceKind>(kind);

	monodroidRuntime.log_traces (env, tk, first_line);
}

#include <string_view>

#include "android-system.hh"
#include "globals.hh"
#include "internal-pinvokes.hh"
#include "jni-remapping.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

int
monodroid_get_system_property (const char *name, char **value)
{
    return AndroidSystem::monodroid_get_system_property (name, value);
}

int
monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix)
{
	EmbeddedAssemblies::set_assemblies_prefix (prefix);
	return 0;
}

void
monodroid_log (LogLevel level, LogCategories category, const char *message)
{
	switch (level) {
		case LogLevel::Verbose:
		case LogLevel::Debug:
			log_debug_nocheck (category, std::string_view { message });
			break;

		case LogLevel::Info:
			log_info_nocheck (category, std::string_view { message });
			break;

		case LogLevel::Warn:
		case LogLevel::Silent: // warn is always printed
			log_warn (category, std::string_view { message });
			break;

		case LogLevel::Error:
			log_error (category, std::string_view { message });
			break;

		case LogLevel::Fatal:
			log_fatal (category, std::string_view { message });
			break;

		default:
		case LogLevel::Unknown:
		case LogLevel::Default:
			log_info_nocheck (category, std::string_view { message });
			break;
	}
}

void
monodroid_free (void *ptr)
{
    free (ptr);
}

int
_monodroid_max_gref_get ()
{
    return static_cast<int>(AndroidSystem::get_max_gref_count ());
}

void
_monodroid_register_reference_logging_callbacks (reference_log_fn log_callback, reference_log_message_fn message_callback)
{
	osBridge.set_reference_logging_callbacks (log_callback, message_callback);
}

void
_monodroid_gc_wait_for_bridge_processing ()
{
    mono_gc_wait_for_bridge_processing ();
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
	ret->start = FastTiming::get_time ();

	return ret;
}

void
monodroid_timing_stop (managed_timing_sequence *sequence, const char *message)
{
	static constexpr const char DEFAULT_MESSAGE[] = "Managed Timing";

	if (sequence == nullptr)
		return;

	sequence->end = FastTiming::get_time ();
	Timing::info (sequence, message == nullptr ? DEFAULT_MESSAGE : message);
	timing->release_sequence (sequence);
}

char**
monodroid_strsplit (const char *str, const char *delimiter, size_t max_tokens)
{
	return Util::monodroid_strsplit (str, delimiter, max_tokens);
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
	return MonodroidRuntime::get_java_class_name_for_TypeManager (klass);
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
 */
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

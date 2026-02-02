#include <host/gc-bridge.hh>
#include <host/host.hh>
#include <host/os-bridge.hh>
#include <host/typemap.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/internal-pinvokes.hh>
#include <runtime-base/jni-remapping.hh>

using namespace xamarin::android;

int _monodroid_gref_get () noexcept
{
	return OSBridge::get_gc_gref_count ();
}

void _monodroid_gref_log (const char *message) noexcept
{
	OSBridge::_monodroid_gref_log (message);
}

int _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, [[maybe_unused]] int from_writable) noexcept
{
	return OSBridge::_monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from);
}

void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, [[maybe_unused]] int from_writable) noexcept
{
	OSBridge::_monodroid_gref_log_delete (handle, type, threadName, threadId, from);
}

void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, [[maybe_unused]] int from_writable)
{
	OSBridge::_monodroid_weak_gref_delete (handle, type, threadName, threadId, from);
}

BridgeProcessingFtn clr_gc_bridge_initialize_for_managed_processing (OnMarkCrossReferencesCallback callback) noexcept
{
	return GCBridge::initialize_for_managed_processing (callback);
}

void monodroid_log (LogLevel level, LogCategories category, const char *message) noexcept
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

char* monodroid_TypeManager_get_java_class_name (jclass klass) noexcept
{
	return Host::get_java_class_name_for_TypeManager (klass);
}

void monodroid_free (void *ptr) noexcept
{
	free (ptr);
}

void _monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, [[maybe_unused]] int from_writable)
{
	OSBridge::_monodroid_weak_gref_new (curHandle, curType, newHandle, newType, threadName, threadId, from);
}

int _monodroid_weak_gref_get ()
{
	return OSBridge::get_gc_weak_gref_count ();
}

int _monodroid_max_gref_get ()
{
	return static_cast<int>(AndroidSystem::get_max_gref_count ());
}

void _monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, [[maybe_unused]] int from_writable)
{
	OSBridge::_monodroid_lref_log_new (lrefc, handle, type, threadName, threadId, from);
}

void _monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, [[maybe_unused]] int from_writable)
{
	OSBridge::_monodroid_lref_log_delete (lrefc, handle, type, threadName, threadId, from);
}

void _monodroid_gc_wait_for_bridge_processing ()
{
	// TODO do we need this method?
	Helpers::abort_application (LOG_DEFAULT, "The method _monodroid_gc_wait_for_bridge_processing is not implemented. This is a stub and should not be called."sv);
}

void _monodroid_detect_cpu_and_architecture (uint16_t *built_for_cpu, uint16_t *running_on_cpu, unsigned char *is64bit)
{
	abort_if_invalid_pointer_argument (built_for_cpu, "built_for_cpu");
	abort_if_invalid_pointer_argument (running_on_cpu, "running_on_cpu");
	abort_if_invalid_pointer_argument (is64bit, "is64bit");

	bool _64bit;
	monodroid_detect_cpu_and_architecture (*built_for_cpu, *running_on_cpu, _64bit);
	*is64bit = _64bit;
}

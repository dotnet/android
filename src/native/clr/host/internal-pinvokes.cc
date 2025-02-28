#include <host/host.hh>
#include <host/os-bridge.hh>
#include <host/typemap.hh>
#include <runtime-base/internal-pinvokes.hh>

using namespace xamarin::android;

int _monodroid_gref_get () noexcept
{
	return OSBridge::get_gc_gref_count ();
}

void _monodroid_gref_log (const char *message) noexcept
{
}

int _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable) noexcept
{
	return OSBridge::_monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}

void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable) noexcept
{
}

const char* clr_typemap_managed_to_java (const char *typeName, const uint8_t *mvid) noexcept
{
	return TypeMapper::typemap_managed_to_java (typeName, mvid);
}

bool clr_typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept
{
	return TypeMapper::typemap_java_to_managed (java_type_name, assembly_name, managed_type_token_id);
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

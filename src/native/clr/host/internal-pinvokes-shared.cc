#include <host/gc-bridge.hh>
#include <host/host-common.hh>
#include <host/typemap.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/internal-pinvokes.hh>
#include <runtime-base/jni-remapping.hh>

using namespace xamarin::android;

BridgeProcessingFtn clr_initialize_gc_bridge (BridgeProcessingFtn bridge_processing_callback) noexcept
{
	return GCBridge::initialize_callback (bridge_processing_callback);
}

void monodroid_log (LogLevel level, LogCategories category, const char *message) noexcept
{
	switch (level) {
		case LogLevel::Verbose:
		case LogLevel::Debug:
			log_debugf (category, "%s", message);
			break;

		case LogLevel::Info:
			log_infof (category, "%s", message);
			break;

		case LogLevel::Warn:
		case LogLevel::Silent: // warn is always printed
			log_write (category, LogLevel::Warn, message);
			break;

		case LogLevel::Error:
			log_write (category, LogLevel::Error, message);
			break;

		case LogLevel::Fatal:
			log_write (category, LogLevel::Fatal, message);
			break;

		default:
		case LogLevel::Unknown:
		case LogLevel::Default:
			log_infof (category, "%s", message);
			break;
	}
}

char* monodroid_TypeManager_get_java_class_name (jclass klass) noexcept
{
	return HostCommon::get_java_class_name_for_TypeManager (klass);
}

void monodroid_free (void *ptr) noexcept
{
	free (ptr);
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

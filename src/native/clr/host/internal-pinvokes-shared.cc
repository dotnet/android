#include <host/gc-bridge.hh>
#include <host/host-common.hh>
#include <host/os-bridge.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/internal-pinvokes.hh>
#include <runtime-base/jni-remapping.hh>

using namespace xamarin::android;

BridgeProcessingFtn clr_initialize_gc_bridge (
	BridgeProcessingStartedFtn bridge_processing_started_callback,
	BridgeProcessingFinishedFtn bridge_processing_finished_callback) noexcept
{
	return GCBridge::initialize_callback (bridge_processing_started_callback, bridge_processing_finished_callback);
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

void _monodroid_register_reference_logging_callbacks (reference_log_fn log_callback, reference_log_message_fn message_callback, uint8_t log_reference_metadata) noexcept
{
	OSBridge::set_reference_logging_callbacks (log_callback, message_callback, log_reference_metadata != 0);
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

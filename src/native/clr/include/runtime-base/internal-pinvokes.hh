#pragma once

#include <jni.h>
#include <ifaddrs.h>
#include <managed-interface.hh>

#include <host/gc-bridge.hh>
#include <xamarin-app.hh>
#include "logger.hh"
#include <runtime-base/timing.hh>

extern "C" {
	BridgeProcessingFtn clr_initialize_gc_bridge (
		BridgeProcessingStartedFtn bridge_processing_started_callback,
		BridgeProcessingFinishedFtn mark_cross_references_callback) noexcept;
	void monodroid_log (xamarin::android::LogLevel level, LogCategories category, const char *message) noexcept;
	char* monodroid_TypeManager_get_java_class_name (jclass klass) noexcept;
	void monodroid_free (void *ptr) noexcept;
	const char* _monodroid_lookup_replacement_type (const char *jniSimpleReference);
	const JniRemappingReplacementMethod* _monodroid_lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature);
	xamarin::android::managed_timing_sequence* monodroid_timing_start (const char *message);
	void monodroid_timing_stop (xamarin::android::managed_timing_sequence *sequence, const char *message);

	void _monodroid_register_reference_logging_callbacks (xamarin::android::reference_log_fn log_callback, xamarin::android::reference_log_message_fn message_callback, uint8_t log_reference_metadata) noexcept;
	void _monodroid_gc_wait_for_bridge_processing ();
	void _monodroid_detect_cpu_and_architecture (unsigned short *built_for_cpu, unsigned short *running_on_cpu, unsigned char *is64bit);
}

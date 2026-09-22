#pragma once

#include <jni.h>
#include <ifaddrs.h>
#include <managed-interface.hh>

#include <host/gc-bridge.hh>
#include <xamarin-app.hh>
#include <runtime-base/logger.hh>

namespace xamarin::android {
	struct managed_timing_sequence;
}

extern "C" {
	const char* clr_typemap_managed_to_java (const char *typeName, const char *assemblyFullName, const uint8_t *mvid) noexcept;
	bool clr_typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept;
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

	int _monodroid_max_gref_get () noexcept;
	void _monodroid_register_reference_logging_callbacks (xamarin::android::reference_log_fn log_callback, xamarin::android::reference_log_message_fn message_callback) noexcept;
	void _monodroid_gc_wait_for_bridge_processing ();
	void _monodroid_detect_cpu_and_architecture (unsigned short *built_for_cpu, unsigned short *running_on_cpu, unsigned char *is64bit);
}

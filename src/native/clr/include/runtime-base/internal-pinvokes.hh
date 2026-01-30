#pragma once

#include <jni.h>
#include <ifaddrs.h>

#include <host/gc-bridge.hh>
#include <xamarin-app.hh>
#include "logger.hh"
#include <runtime-base/timing.hh>

extern "C" {
	int _monodroid_gref_get () noexcept;
	void _monodroid_gref_log (const char *message) noexcept;
	int _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable) noexcept;
	void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable) noexcept;
	const char* clr_typemap_managed_to_java (const char *typeName, const uint8_t *mvid) noexcept;
	bool clr_typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept;
	BridgeProcessingFtn clr_initialize_gc_bridge (
		BridgeProcessingStartedFtn bridge_processing_started_callback,
		BridgeProcessingFinishedFtn mark_cross_references_callback) noexcept;

	// Functions for managed GC bridge processing mode
	BridgeProcessingFtn clr_gc_bridge_initialize_for_managed_processing () noexcept;
	MarkCrossReferencesArgs* clr_gc_bridge_wait_for_processing () noexcept;
	void clr_gc_bridge_trigger_java_gc () noexcept;

	void monodroid_log (xamarin::android::LogLevel level, LogCategories category, const char *message) noexcept;
	char* monodroid_TypeManager_get_java_class_name (jclass klass) noexcept;
	void monodroid_free (void *ptr) noexcept;
	const char* _monodroid_lookup_replacement_type (const char *jniSimpleReference);
	const JniRemappingReplacementMethod* _monodroid_lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature);
	xamarin::android::managed_timing_sequence* monodroid_timing_start (const char *message);
	void monodroid_timing_stop (xamarin::android::managed_timing_sequence *sequence, const char *message);

	void _monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable);
	int _monodroid_weak_gref_get ();
	int _monodroid_max_gref_get ();
	void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);

	void _monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
	void _monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char  *from, int from_writable);
	void _monodroid_gc_wait_for_bridge_processing ();
	void _monodroid_detect_cpu_and_architecture (unsigned short *built_for_cpu, unsigned short *running_on_cpu, unsigned char *is64bit);
}

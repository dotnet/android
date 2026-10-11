#pragma once

#include <jni.h>
#include <ifaddrs.h>
#include <managed-interface.hh>

#include <host/gc-bridge.hh>
#include <xamarin-app.hh>
#include <shared/log_types.hh>

extern "C" {
	BridgeProcessingFtn clr_initialize_gc_bridge (BridgeProcessingFtn bridge_processing_callback) noexcept;
	void monodroid_log (xamarin::android::LogLevel level, LogCategories category, const char *message) noexcept;
	char* monodroid_TypeManager_get_java_class_name (jclass klass) noexcept;
	void monodroid_free (void *ptr) noexcept;

	void _monodroid_detect_cpu_and_architecture (unsigned short *built_for_cpu, unsigned short *running_on_cpu, unsigned char *is64bit);
}

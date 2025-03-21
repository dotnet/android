#pragma once

#include <jni.h>
#include <ifaddrs.h>

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
	void monodroid_log (xamarin::android::LogLevel level, LogCategories category, const char *message) noexcept;
	char* monodroid_TypeManager_get_java_class_name (jclass klass) noexcept;
	void monodroid_free (void *ptr) noexcept;
	const char* _monodroid_lookup_replacement_type (const char *jniSimpleReference);
	const JniRemappingReplacementMethod* _monodroid_lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature);
}


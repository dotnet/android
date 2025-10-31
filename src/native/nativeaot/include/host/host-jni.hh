#pragma once

#include <jni.h>

extern "C" {
	[[gnu::visibility("default")]]
	auto XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved) -> int;
	void XA_Host_NativeAOT_OnInit (jstring language, jstring filesDir, jstring cacheDir);
	uint32_t XA_Host_NativeAOT_GetLoggingCategories ();
}

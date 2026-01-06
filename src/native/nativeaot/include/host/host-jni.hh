#pragma once

#include <jni.h>
#include <managed-interface.hh>

extern "C" {
	[[gnu::visibility("default")]]
	auto XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved) -> int;
	void XA_Host_NativeAOT_OnInit (jstring language, jstring filesDir, jstring cacheDir, xamarin::android::JnienvInitializeArgs *initArgs);
}

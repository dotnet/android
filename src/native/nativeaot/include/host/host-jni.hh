#pragma once

#include <jni.h>

extern "C" {
	[[gnu::visibility("default")]]
	auto XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved) -> int;
}

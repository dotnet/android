#include <jni.h>

#include <host/host-jni.hh>
#include <host/host-nativeaot.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

// External weak symbol defined in LLVM IR marshal methods (marshal_methods_*.ll)
// This is where we store the managed callback for resolving marshal method function pointers
extern "C" [[gnu::weak]] get_function_pointer_typemap_fn typemap_get_function_pointer;

auto XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved) -> int
{
	return Host::Java_JNI_OnLoad (vm, reserved);
}

void XA_Host_NativeAOT_OnInit (jstring language, jstring filesDir, jstring cacheDir, JnienvInitializeArgs *initArgs)
{
	Host::OnInit (language, filesDir, cacheDir, initArgs);
}

void XA_Host_NativeAOT_SetTypemapGetFunctionPointer (get_function_pointer_typemap_fn getFunctionPointerFn)
{
	if (getFunctionPointerFn == nullptr) {
		log_warn (LOG_DEFAULT, "XA_Host_NativeAOT_SetTypemapGetFunctionPointer called with null function pointer");
		return;
	}

	if (&typemap_get_function_pointer != nullptr) {
		typemap_get_function_pointer = getFunctionPointerFn;
		log_debug (LOG_DEFAULT, "Type Mapping API typemap_get_function_pointer callback set");
	} else {
		log_warn (LOG_DEFAULT, "Type Mapping API callback provided but typemap_get_function_pointer symbol not found");
	}
}

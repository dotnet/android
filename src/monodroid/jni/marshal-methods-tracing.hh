#if !defined (__MARSHAL_METHODS_TRACING_HH)
#define __MARSHAL_METHODS_TRACING_HH

#include <jni.h>

#include "monodroid-glue-internal.hh"

// These values MUST match those in the MarshalMethodsTracingMode managed enum (src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsTracingMode.cs)

inline constexpr int32_t TracingModeNone  = 0x00;
inline constexpr int32_t TracingModeBasic = 0x01;
inline constexpr int32_t TracingModeFull  = 0x02;

extern "C" {
	void _mm_trace_init (JNIEnv *env) noexcept;
	void _mm_trace (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* method_name, const char* message) noexcept;
	void _mm_trace_func_enter (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name) noexcept;
	void _mm_trace_func_leave (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name) noexcept;
}

#endif // ndef __MARSHAL_METHODS_TRACING_HH

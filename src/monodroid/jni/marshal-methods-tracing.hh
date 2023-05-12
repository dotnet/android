#include <cstdint>
#if !defined (__MARSHAL_METHODS_TRACING_HH)
#define __MARSHAL_METHODS_TRACING_HH

#include <jni.h>

#include "monodroid-glue-internal.hh"

// These values MUST match those in the MarshalMethodsTracingMode managed enum (src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsTracingMode.cs)

inline constexpr int32_t TracingModeNone  = 0x00;
inline constexpr int32_t TracingModeBasic = 0x01;
inline constexpr int32_t TracingModeFull  = 0x02;

extern "C" {
	[[gnu::visibility("hidden")]]
	void _mm_trace (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* method_name, const char* message) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_func_enter (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name, const char* method_params) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_func_leave (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name, const char* method_params) noexcept;

	// Returns pointer to a constant string, must not be freed
	[[gnu::visibility("hidden")]]
	const char* _mm_trace_render_bool (bool v) noexcept;

	// Returns pointer to a dynamically allocated string, must be freed
	[[gnu::visibility("hidden")]]
	const char* _mm_trace_render_java_string (JNIEnv *env, jstring v) noexcept;
}

#endif // ndef __MARSHAL_METHODS_TRACING_HH

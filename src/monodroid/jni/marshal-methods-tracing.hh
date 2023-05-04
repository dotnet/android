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
	struct MethodParams;

	[[gnu::visibility("hidden")]]
	void _mm_trace (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* method_name, const char* message) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_func_enter (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_func_leave (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name) noexcept;

	[[gnu::visibility("hidden")]]
	MethodParams* _mm_trace_method_params_new (JNIEnv *env, jclass klass) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_method_params_destroy (MethodParams *v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_bool (MethodParams *state, bool v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_byte (MethodParams *state, uint8_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_sbyte (MethodParams *state, int8_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_char (MethodParams *state, char v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_short (MethodParams *state, int16_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_ushort (MethodParams *state, uint16_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_int (MethodParams *state, int32_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_uint (MethodParams *state, uint32_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_long (MethodParams *state, int64_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_ulong (MethodParams *state, uint64_t v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_float (MethodParams *state, float v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_double (MethodParams *state, double v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_string (MethodParams *state, JNIEnv *env, jstring v) noexcept;

	[[gnu::visibility("hidden")]]
	void _mm_trace_param_append_pointer (MethodParams *state, void* v) noexcept;
}

#endif // ndef __MARSHAL_METHODS_TRACING_HH

#include <array>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <memory>
#include <string>
#include <type_traits>

#include <android/log.h>

#include "marshal-methods-tracing.hh"
#include "marshal-methods-utilities.hh"
#include "cpp-util.hh"
#include "helpers.hh"
#include "native-tracing.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

constexpr int PRIORITY = ANDROID_LOG_INFO;
constexpr char LEAD[] = "MM: ";

struct MethodParams
{
	std::string buffer;
};

void _mm_trace (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* method_name, const char *message) noexcept
{

}

static void _mm_trace_func_leave_enter (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token,
                                        const char* which, const char* native_method_name, bool need_trace) noexcept
{
	uint64_t method_id = MarshalMethodsUtilities::get_method_id (mono_image_index, method_token);
	const char *managed_method_name = MarshalMethodsUtilities::get_method_name (method_id);
	const char *class_name = MarshalMethodsUtilities::get_class_name (class_index);

	if (need_trace && tracing_mode == TracingModeFull) {
		std::string trace { LEAD };
		trace.append (which);
		trace.append (": ");
		trace.append (native_method_name);
		trace.append (" (");
		trace.append (managed_method_name);
		trace.append (") in class ");
		trace.append (class_name);

		trace.append ("\n  Native stack trace:\n");
		c_unique_ptr<const char> native_trace { xa_get_native_backtrace () };
		trace.append (native_trace.get ());
		trace.append ("\n");

		trace.append ("\n  Java stack trace:\n");
		c_unique_ptr<const char> java_trace { xa_get_java_backtrace (env) };
		trace.append (java_trace.get ());
		trace.append ("\n");

		trace.append ("\n  Installed signal handlers:\n");
		c_unique_ptr<const char> signal_handlers { xa_get_interesting_signal_handlers () };
		trace.append (signal_handlers.get ());

		__android_log_write (PRIORITY, SharedConstants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY, trace.c_str ());
	} else {
		__android_log_print (
			PRIORITY,
			SharedConstants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY,
			"%s%s: %s (%s) in class %s",
			LEAD,
			which,
			native_method_name,
			managed_method_name,
			class_name
		);
	}
}

void _mm_trace_func_enter (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name) noexcept
{
	constexpr char ENTER[] = "ENTER";
	_mm_trace_func_leave_enter (env, tracing_mode, mono_image_index, class_index, method_token, ENTER, native_method_name, true /* need_trace */);
}

void _mm_trace_func_leave (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name) noexcept
{
	constexpr char LEAVE[] = "LEAVE";
	_mm_trace_func_leave_enter (env, tracing_mode, mono_image_index, class_index, method_token, LEAVE, native_method_name, false /* need_trace */);
}

template<typename TVal>
[[gnu::always_inline]]
static void append_param (std::string& buffer, TVal value) noexcept
{
	if (!buffer.empty ()) {
		buffer.append (", ");
	}

	buffer.append (value);
}

template<typename TVal>
concept AcceptableInteger = std::is_integral_v<TVal> || std::is_pointer_v<TVal>;

template<AcceptableInteger TVal>
[[gnu::always_inline]]
static void append_hex_integer (std::string& buffer, TVal value) noexcept
{
	const char *format;

	if constexpr (std::is_same_v<TVal, int64_t> || std::is_same_v<TVal, uint64_t>) {
		format = "0x%lx";
	} else if constexpr (std::is_pointer_v<TVal>) {
		format = "%p";
	} else {
		format = "0x%x";
	}

	// Enough room for maximum decimal representation of 64-bit unsigned value, or a minimum signed one with the sign.
	std::array<char, std::numeric_limits<uint64_t>::digits10 + 2> data_buf;
	std::snprintf (data_buf.data (), data_buf.size (), format, value);
	append_param (buffer, data_buf.data ());
}

[[gnu::always_inline]]
static void append_pointer (std::string& buffer, void *pointer) noexcept
{
	append_hex_integer (buffer, pointer);
}

MethodParams* _mm_trace_method_params_new (JNIEnv *env, jclass klass) noexcept
{
	auto ret = new MethodParams ();

	append_pointer (ret->buffer, env);
	append_pointer (ret->buffer, klass);

	return ret;
}

void _mm_trace_method_params_destroy (MethodParams *v) noexcept
{
	if (v == nullptr) {
		return;
	}

	delete v;
}

void _mm_trace_param_append_bool (MethodParams *state, bool v) noexcept
{
	if (state == nullptr) {
		return;
	}

	append_param (state->buffer, v ? "true" : "false");
}

template<AcceptableInteger TVal>
[[gnu::always_inline]]
static void _mm_trace_param_append_hex_integer (MethodParams *state, TVal v) noexcept
{
	if (state == nullptr) {
		return;
	}

	append_hex_integer (state->buffer, v);
}

void _mm_trace_param_append_byte (MethodParams *state, uint8_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_sbyte (MethodParams *state, int8_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_char (MethodParams *state, char v) noexcept
{
	if (state == nullptr) {
		return;
	}

	state->buffer.append ("'");
	state->buffer.append (1, v);
	state->buffer.append ("'");
}

void _mm_trace_param_append_short (MethodParams *state, int16_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_ushort (MethodParams *state, uint16_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_int (MethodParams *state, int32_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_uint (MethodParams *state, uint32_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_long (MethodParams *state, int64_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_ulong (MethodParams *state, uint64_t v) noexcept
{
	_mm_trace_param_append_hex_integer (state, v);
}

void _mm_trace_param_append_float (MethodParams *state, float v) noexcept
{
	if (state == nullptr) {
		return;
	}

	state->buffer.append (std::to_string (v));
}

void _mm_trace_param_append_double (MethodParams *state, double v) noexcept
{
	if (state == nullptr) {
		return;
	}

	state->buffer.append (std::to_string (v));
}

void _mm_trace_param_append_string (MethodParams *state, JNIEnv *env, jstring v) noexcept
{
	if (state == nullptr) {
		return;
	}

	if (env == nullptr) {
		state->buffer.append ("\"<missing env>\"");
		return;
	}

	const char *s = env->GetStringUTFChars (v, nullptr);

	state->buffer.append ("\"");
	state->buffer.append (s);
	state->buffer.append ("\"");

	env->ReleaseStringUTFChars (v, s);
}

void _mm_trace_param_append_pointer (MethodParams *state, void* v) noexcept
{
	if (state == nullptr) {
		return;
	}

	append_pointer (state->buffer, v);
}

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
constexpr char BOOL_TRUE[] = "true";
constexpr char BOOL_FALSE[] = "false";
constexpr char MISSING_ENV[] = "<missing env>";

void _mm_trace (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* method_name, const char *message) noexcept
{

}

static void _mm_trace_func_leave_enter (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token,
                                        const char* which, const char* native_method_name, bool need_trace, const char* method_extra) noexcept
{
	uint64_t method_id = MarshalMethodsUtilities::get_method_id (mono_image_index, method_token);
	const char *managed_method_name = MarshalMethodsUtilities::get_method_name (method_id);
	const char *class_name = MarshalMethodsUtilities::get_class_name (class_index);

	if (need_trace && tracing_mode == TracingModeFull) {
		std::string trace { LEAD };
		trace.append (which);
		trace.append (": ");
		trace.append (native_method_name);
		if (method_extra != nullptr) {
			trace.append (" ");
			trace.append (method_extra);
		}
		trace.append (" {");
		trace.append (managed_method_name);
		trace.append ("} in class ");
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
			"%s%s: %s%s%s {%s} in class %s",
			LEAD,
			which,
			native_method_name,
			method_extra == nullptr ? "" : " ",
			method_extra == nullptr ? "" : method_extra,
			managed_method_name,
			class_name
		);
	}
}

void _mm_trace_func_enter (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name, const char* method_params) noexcept
{
	constexpr char ENTER[] = "ENTER";
	_mm_trace_func_leave_enter (env, tracing_mode, mono_image_index, class_index, method_token, ENTER, native_method_name, true /* need_trace */, method_params);
}

void _mm_trace_func_leave (JNIEnv *env, int32_t tracing_mode, uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name, const char* method_return_value) noexcept
{
	constexpr char LEAVE[] = "LEAVE";
	_mm_trace_func_leave_enter (env, tracing_mode, mono_image_index, class_index, method_token, LEAVE, native_method_name, false /* need_trace */, method_return_value);
}

const char* _mm_trace_render_bool (bool v) noexcept
{
	return v ? BOOL_TRUE : BOOL_FALSE;
}

const char* _mm_trace_render_java_string (JNIEnv *env, jstring v) noexcept
{
	if (env == nullptr) {
		return strdup (MISSING_ENV);
	}

	const char *s = env->GetStringUTFChars (v, nullptr);
	const char *ret = strdup (s);
	env->ReleaseStringUTFChars (v, s);

	return ret;
}

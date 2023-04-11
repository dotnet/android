#if !defined (__NATIVE_TRACING_HH)
#define __NATIVE_TRACING_HH

#include <string>
#include <jni.h>
#include <android/log.h>

#define UNW_LOCAL_ONLY
#include <libunwind.h>

namespace xamarin::android::internal
{
	class NativeTracing final
	{
		static constexpr int PRIORITY = ANDROID_LOG_INFO;

	public:
		static void get_native_backtrace (std::string& trace) noexcept;
		static void get_java_backtrace (JNIEnv *env, std::string &trace) noexcept;
		static void get_interesting_signal_handlers (std::string &trace) noexcept;

	private:
		static void append_frame_number (std::string &trace, size_t count) noexcept;
		static unw_word_t adjust_address (unw_word_t addr) noexcept;
		static void init_jni (JNIEnv *env) noexcept;
		static bool assert_valid_jni_pointer (void *o, const char *missing_kind, const char *missing_name) noexcept;

		template<class TJavaPointer>
		static TJavaPointer to_gref (JNIEnv *env, TJavaPointer lref) noexcept;

	private:
		// java.lang.Thread
		inline static jclass java_lang_Thread;
		inline static jmethodID java_lang_Thread_currentThread;
		inline static jmethodID java_lang_Thread_getStackTrace;

		// java.lang.StackTraceElement
		inline static jclass java_lang_StackTraceElement;
		inline static jmethodID java_lang_StackTraceElement_toString;
	};
}
#endif // ndef __NATIVE_TRACING_HH

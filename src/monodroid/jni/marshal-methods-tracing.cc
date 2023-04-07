#include <array>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <string>

#include <signal.h>
#include <dlfcn.h>
#include <cxxabi.h>

#include <android/log.h>

#define UNW_LOCAL_ONLY
#include <libunwind.h>

#include "marshal-methods-tracing.hh"
#include "marshal-methods-utilities.hh"
#include "helpers.hh"

using namespace xamarin::android::internal;

constexpr int PRIORITY = ANDROID_LOG_INFO;
constexpr char LEAD[] = "MM: ";

// java.lang.Thread
static jclass java_lang_Thread;
static jmethodID java_lang_Thread_currentThread;
static jmethodID java_lang_Thread_getStackTrace;

// java.lang.StackTraceElement
static jclass java_lang_StackTraceElement;
static jmethodID java_lang_StackTraceElement_toString;

// TODO: implement Java trace (use similar approach as our managed code uses, by instantiating a Java exception)
//      https://developer.android.com/reference/java/lang/Error?hl=en and use `getStackTrace`

// TODO: see if MonoVM could implement https://www.nongnu.org/libunwind/man/libunwind-dynamic(3).html
//

// https://www.nongnu.org/libunwind/docs.html

[[gnu::always_inline]]
unw_word_t adjust_address (unw_word_t addr) noexcept
{
	// This is what bionic does, let's do the same so that our backtrace addresses match bionic output
	// Code copied verbatim from
	//    https://android.googlesource.com/platform/bionic/+/refs/tags/android-13.0.0_r37/libc/bionic/execinfo.cpp#50
	if (addr != 0) {
#if defined (__arm__)
		// If the address is suspiciously low, do nothing to avoid a segfault trying
		// to access this memory.
		if (addr >= 4096) {
			// Check bits [15:11] of the first halfword assuming the instruction
			// is 32 bits long. If the bits are any of these values, then our
			// assumption was correct:
			//  b11101
			//  b11110
			//  b11111
			// Otherwise, this is a 16 bit instruction.
			uint16_t value = (*reinterpret_cast<uint16_t*>(addr - 2)) >> 11;
			if (value == 0x1f || value == 0x1e || value == 0x1d) {
				return addr - 4;
			}

			return addr - 2;
		}
#elif defined (__aarch64__)
		// All instructions are 4 bytes long, skip back one instruction.
		return addr - 4;
#elif defined (__i386__) || defined (__x86_64__)
		// It's difficult to decode exactly where the previous instruction is,
		// so subtract 1 to estimate where the instruction lives.
		return addr - 1;
#endif
	}

	return addr;
}

[[gnu::always_inline]]
static void append_frame_number (std::string &trace, size_t count) noexcept
{
	std::array<char, 32>   num_buf; // Enough for text representation of a decimal 64-bit integer + some possible
									// additions (sign, padding, punctuation etc)
	trace.append ("    #");
	std::snprintf (num_buf.data (), num_buf.size (), "%-3zu: ", count);
	trace.append (num_buf.data ());
}

[[gnu::always_inline]]
static void get_native_backtrace (std::string& trace) noexcept
{
	constexpr int FRAME_OFFSET_WIDTH = sizeof(uintptr_t) * 2;

	unw_cursor_t           cursor;
	unw_context_t          uc;
	unw_word_t             ip;
	unw_word_t             offp;
	std::array<char, 512>  name_buf;
	std::array<char, 32>   num_buf; // Enough for text representation of a decimal 64-bit integer + some possible
									// additions (sign, padding, punctuation etc)
	const char            *symbol_name;
	Dl_info                info;

	unw_getcontext (&uc);
	unw_init_local (&cursor, &uc);

	size_t frame_counter = 0;

	trace.append ("\n  Native stack trace:");
	while (unw_step (&cursor) > 0) {
		trace.append ("\n");

		unw_get_reg (&cursor, UNW_REG_IP, &ip);
		ip = adjust_address (ip);

		auto ptr = reinterpret_cast<void*>(ip);
		const char *fname = nullptr;
		const void *symptr = nullptr;
		unw_word_t  frame_offset = 0;
		bool info_valid = false;

		if (dladdr (ptr, &info) != 0) {
			if (info.dli_fname != nullptr) {
				fname = info.dli_fname;
			}
			symptr = info.dli_sname;
			frame_offset = ip - reinterpret_cast<unw_word_t>(info.dli_fbase);
			info_valid = true;
		} else {
			frame_offset = ip;
		}

		append_frame_number (trace, frame_counter++);

		std::snprintf (num_buf.data (), num_buf.size (), "%0*zx (", FRAME_OFFSET_WIDTH, frame_offset);
		trace.append (num_buf.data ());
		std::snprintf (num_buf.data (), num_buf.size (), "%p) ", ptr);
		trace.append (num_buf.data ());

		// TODO: consider searching /proc/self/maps for the beginning of the corresponding region to calculate the
		// correct offset (like done in bionic stack trace)
		trace.append (fname != nullptr ? fname : "[anonymous]");

		bool symbol_name_allocated = false;
		offp = 0;
		if (unw_get_proc_name (&cursor, name_buf.data (), name_buf.size (), &offp) == 0) {
			symbol_name = name_buf.data ();
		} else if (info_valid && info.dli_sname != nullptr) {
			symbol_name = info.dli_sname;
		} else {
			symbol_name = nullptr;
		}
		offp = adjust_address (offp);

		if (symbol_name != nullptr) {
			char *demangled_symbol_name;
			int demangle_status;

			// https://itanium-cxx-abi.github.io/cxx-abi/abi.html#demangler
			demangled_symbol_name = abi::__cxa_demangle (symbol_name, nullptr, nullptr, &demangle_status);
			symbol_name_allocated = demangle_status == 0 && demangled_symbol_name != nullptr;
			if (symbol_name_allocated) {
				symbol_name = demangled_symbol_name;
			}
		}

		if (symbol_name != nullptr) {
			trace.append (" ");
			trace.append (symbol_name);
			if (offp != 0) {
				trace.append (" + ");
				std::snprintf (num_buf.data (), num_buf.size (), "%zu", offp);
				trace.append (num_buf.data ());
			}
		}

		if (symptr != nullptr) {
			trace.append (" (symaddr: ");
			std::snprintf (num_buf.data (), num_buf.size (), "%p", symptr);
			trace.append (num_buf.data ());
			trace.append (")");
		}

		if (symbol_name_allocated && symbol_name != nullptr) {
			std::free (reinterpret_cast<void*>(const_cast<char*>(symbol_name)));
		}
	}
}

[[gnu::always_inline]]
static void get_java_backtrace (JNIEnv *env, std::string &trace) noexcept
{
	// TODO: error handling
	trace.append ("\n  Java stack trace:");
	jobject current_thread = env->CallStaticObjectMethod (java_lang_Thread, java_lang_Thread_currentThread);
	auto stack_trace_array = static_cast<jobjectArray>(env->CallNonvirtualObjectMethod (current_thread, java_lang_Thread, java_lang_Thread_getStackTrace));
	jsize nframes = env->GetArrayLength (stack_trace_array);

	for (jsize i = 0; i < nframes; i++) {
		jobject frame = env->GetObjectArrayElement (stack_trace_array, i);
		auto frame_desc_java = static_cast<jstring>(env->CallNonvirtualObjectMethod (frame, java_lang_StackTraceElement, java_lang_StackTraceElement_toString));
		const char *frame_desc = env->GetStringUTFChars (frame_desc_java, nullptr);

		trace.append ("\n");
		append_frame_number (trace, i);
		trace.append (frame_desc);
		env->ReleaseStringUTFChars (frame_desc_java, frame_desc);
	}
}

[[gnu::always_inline]]
static void get_interesting_signal_handlers (std::string &trace) noexcept
{
	constexpr char SA_SIGNAL[] = "signal";
	constexpr char SA_SIGACTION[] = "sigaction";
	constexpr char SIG_IGNORED[] = "[ignored]";

	trace.append ("\n  Signal handlers:");

	std::array<char, 32>   num_buf;
	Dl_info info;
	struct sigaction cur_sa;
	for (int i = 0; i < _NSIG; i++) {
		if (sigaction (i, nullptr, &cur_sa) != 0) {
			continue; // ignore
		}

		void *handler;
		const char *installed_with;
		if (cur_sa.sa_flags & SA_SIGINFO) {
			handler = reinterpret_cast<void*>(cur_sa.sa_sigaction);
			installed_with = SA_SIGACTION;
		} else {
			handler = reinterpret_cast<void*>(cur_sa.sa_handler);
			installed_with = SA_SIGNAL;
		}

		if (handler == SIG_DFL) {
			continue;
		}

		trace.append ("\n");
		const char *symbol_name = nullptr;
		const char *file_name = nullptr;
		if (handler == SIG_IGN) {
			symbol_name = SIG_IGNORED;
		} else {
			if (dladdr (handler, &info) != 0) {
				symbol_name = info.dli_sname;
				file_name = info.dli_fname;
			}
		}

		trace.append ("    ");
		trace.append (strsignal (i));
		trace.append (" (");
		std::snprintf (num_buf.data (), num_buf.size (), "%d", i);
		trace.append (num_buf.data ());
		trace.append ("), with ");
		trace.append (installed_with);
		trace.append (": ");

		if (file_name != nullptr) {
			trace.append (file_name);
			trace.append (" ");
		}

		if (symbol_name == nullptr) {
			std::snprintf (num_buf.data (), num_buf.size (), "%p", handler);
			trace.append (num_buf.data ());
		} else {
			trace.append (symbol_name);
		}
	}
}

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

		get_native_backtrace (trace);
		trace.append ("\n");
		get_java_backtrace (env, trace);
		trace.append ("\n");
		get_interesting_signal_handlers (trace);

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

static bool assert_valid_pointer (void *o, const char *missing_kind, const char *missing_name) noexcept
{
	if (o != nullptr) {
		return true;
	}

	__android_log_print (
		PRIORITY,
		SharedConstants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY,
		"%smissing Java %s: %s",
		LEAD,
		missing_kind,
		missing_name
	);

	return false;
}

template<class TJavaPointer>
static TJavaPointer to_gref (JNIEnv *env, TJavaPointer lref) noexcept
{
	if (lref == nullptr) {
		return nullptr;
	}

	auto ret = static_cast<TJavaPointer> (env->NewGlobalRef (lref));
	env->DeleteLocalRef (lref);
	return ret;
}

void _mm_trace_init (JNIEnv *env) noexcept
{
	// We might be called more than once, ignore all but the first call
	if (java_lang_Thread != nullptr) {
		return;
	}

	java_lang_Thread = to_gref (env, env->FindClass ("java/lang/Thread"));
	java_lang_Thread_currentThread = env->GetStaticMethodID (java_lang_Thread, "currentThread", "()Ljava/lang/Thread;");
	java_lang_Thread_getStackTrace = env->GetMethodID (java_lang_Thread, "getStackTrace", "()[Ljava/lang/StackTraceElement;");
	java_lang_StackTraceElement = to_gref (env, env->FindClass ("java/lang/StackTraceElement"));
	java_lang_StackTraceElement_toString = env->GetMethodID (java_lang_StackTraceElement, "toString", "()Ljava/lang/String;");

	// We check for the Java exception and possible null pointers only here, since all the calls JNI before the last one
	// would do the exception check for us.
	if (env->ExceptionOccurred ()) {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		xamarin::android::Helpers::abort_application ();
	}

	bool all_found = assert_valid_pointer (java_lang_Thread, "class", "java.lang.Thread");
	all_found &= assert_valid_pointer (java_lang_Thread_currentThread, "method", "java.lang.Thread.currentThread ()");
	all_found &= assert_valid_pointer (java_lang_Thread_getStackTrace, "method", "java.lang.Thread.getStackTrace ()");
	all_found &= assert_valid_pointer (java_lang_Thread, "class", "java.lang.StackTraceElement");
	all_found &= assert_valid_pointer (java_lang_Thread_currentThread, "method", "java.lang.StackTraceElement.toString ()");

	if (!all_found) {
		xamarin::android::Helpers::abort_application ();
	}
}

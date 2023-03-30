#include <cstdlib>
#include <limits>
#include <dlfcn.h>
#include <cxxabi.h>

#include <android/log.h>

#define HIDE_EXPORTS
#include <unwind.h>

#include "marshal-methods-tracing.hh"
#include "marshal-methods-utilities.hh"

struct unw_context_t {
  uint64_t data[_LIBUNWIND_CONTEXT_SIZE];
};
typedef struct unw_context_t unw_context_t;

struct unw_cursor_t {
  uint64_t data[_LIBUNWIND_CURSOR_SIZE];
} LIBUNWIND_CURSOR_ALIGNMENT_ATTR;
typedef struct unw_cursor_t unw_cursor_t;

extern int unw_getcontext(unw_context_t *);
extern int unw_init_local(unw_cursor_t *, unw_context_t *);

using namespace xamarin::android::internal;

constexpr int PRIORITY = ANDROID_LOG_INFO;
constexpr char LEAD[] = "MM: ";

// TODO: implement Java trace (use similar approach as our managed code uses, by instantiating a Java exception)
//      https://developer.android.com/reference/java/lang/Error?hl=en and use `getStackTrace`

static _Unwind_Reason_Code backtrace_frame_callback (_Unwind_Context* context, [[maybe_unused]] void* arg)
{
	int ip_before_instruction = 0;
	uintptr_t ip = _Unwind_GetIPInfo (context, &ip_before_instruction);

	if (ip_before_instruction == 0) {
		if (ip == 0) {
			// It's as if 0 - 1, but without tripping up static analyzers claiming an underflow
			ip = std::numeric_limits<uintptr_t>::max();
		} else {
			ip -= 1;
		}
	}

	auto ptr = reinterpret_cast<void*>(ip);
	Dl_info info {};
	const char *symbol_name = nullptr;
	bool symbol_name_allocated = false;

	if (dladdr (ptr, &info) != 0) {
		char *demangled_symbol_name;
		int demangle_status;

		// https://itanium-cxx-abi.github.io/cxx-abi/abi.html#demangler
		demangled_symbol_name = abi::__cxa_demangle (info.dli_sname, nullptr, nullptr, &demangle_status);
		symbol_name_allocated = demangle_status == 0 && demangled_symbol_name != nullptr;
		symbol_name = symbol_name_allocated ? demangled_symbol_name : info.dli_sname;
	}

	__android_log_print (
		PRIORITY,
		SharedConstants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY,
		"  %p %s %s (%p)",
		ptr,
		info.dli_fname == nullptr ? "[unknown file]" : info.dli_fname,
		symbol_name == nullptr ? "[unknown symbol]" : symbol_name,
		info.dli_saddr
	);

	if (symbol_name_allocated && symbol_name != nullptr) {
		std::free (reinterpret_cast<void*>(const_cast<char*>(symbol_name)));
	}

	return _URC_NO_REASON;
}

static void print_native_backtrace () noexcept
{

	_Unwind_Backtrace (backtrace_frame_callback, nullptr);
}

void _mm_trace (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* method_name, const char *message)
{

}

static void _mm_trace_func_leave_enter (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* which, const char* native_method_name, bool need_trace)
{
	uint64_t method_id = MarshalMethodsUtilities::get_method_id (mono_image_index, method_token);
	const char *managed_method_name = MarshalMethodsUtilities::get_method_name (method_id);
	const char *class_name = MarshalMethodsUtilities::get_class_name (class_index);

	__android_log_print (PRIORITY, SharedConstants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY, "%s%s: %s (%s) in class %s", LEAD, which, native_method_name, managed_method_name, class_name);
	if (need_trace) {
		__android_log_print (PRIORITY, SharedConstants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY, "Native stack trace:");
		print_native_backtrace ();
	}
}

void _mm_trace_func_enter (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name)
{
	constexpr char ENTER[] = "ENTER";
	_mm_trace_func_leave_enter (mono_image_index, class_index, method_token, ENTER, native_method_name, true /* need_trace */);
}

void _mm_trace_func_leave (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* native_method_name)
{
	constexpr char LEAVE[] = "LEAVE";
	_mm_trace_func_leave_enter (mono_image_index, class_index, method_token, LEAVE, native_method_name, false /* need_trace */);
}

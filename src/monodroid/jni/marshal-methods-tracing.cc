#include <array>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <string>

#include <dlfcn.h>
#include <cxxabi.h>

#include <android/log.h>

#define UNW_LOCAL_ONLY
#include <libunwind.h>

#include "marshal-methods-tracing.hh"
#include "marshal-methods-utilities.hh"

using namespace xamarin::android::internal;

constexpr int PRIORITY = ANDROID_LOG_INFO;
constexpr char LEAD[] = "MM: ";

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
static void print_native_backtrace (std::string const& first_line) noexcept
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

	std::string trace {first_line};
	size_t frame_counter = 0;
	bool got_anon = false;

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

		trace.append ("    #");
		std::snprintf (num_buf.data (), num_buf.size (), "%-3zu: ", frame_counter++);
		trace.append (num_buf.data ());
		std::snprintf (num_buf.data (), num_buf.size (), "%0*zx (", FRAME_OFFSET_WIDTH, frame_offset);
		trace.append (num_buf.data ());
		std::snprintf (num_buf.data (), num_buf.size (), "%p) ", ptr);
		trace.append (num_buf.data ());

		// TODO: consider searching /proc/self/maps for the beginning of the corresponding region to calculate the
		// correct offset (like done in bionic stack trace)
		trace.append (fname != nullptr ? fname : "[anonymous]");
		if (fname == nullptr) {
			got_anon = true;
		}

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

	__android_log_write (PRIORITY, SharedConstants::LOG_CATEGORY_NAME_MONODROID_ASSEMBLY, trace.c_str ());
	if (got_anon) {
		std::abort ();
	}
}

void _mm_trace (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* method_name, const char *message)
{

}

static void _mm_trace_func_leave_enter (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, const char* which, const char* native_method_name, bool need_trace)
{
	uint64_t method_id = MarshalMethodsUtilities::get_method_id (mono_image_index, method_token);
	const char *managed_method_name = MarshalMethodsUtilities::get_method_name (method_id);
	const char *class_name = MarshalMethodsUtilities::get_class_name (class_index);

	if (need_trace) {
		std::string first_line { LEAD };
		first_line.append (which);
		first_line.append (": ");
		first_line.append (native_method_name);
		first_line.append (" (");
		first_line.append (managed_method_name);
		first_line.append (") in class ");
		first_line.append (class_name);

		print_native_backtrace (first_line);
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

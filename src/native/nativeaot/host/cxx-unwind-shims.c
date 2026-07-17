#include <stdlib.h>

#include <android/log.h>
#include <unwind.h>

#define UNUSED __attribute__((unused))

// NativeAOT's Android host is built without C++ exception support.  Keep libc++/libc++abi
// available for now, but fail fast instead of linking the NDK's full exception unwinder.
__attribute__((noreturn))
static void abort_cxx_exception_unwind (const char *function)
{
	__android_log_print (
		ANDROID_LOG_FATAL,
		"DOTNET",
		"C++ exception unwinding is not supported by the NativeAOT host: %s",
		function
	);
	abort ();
}

void _Unwind_DeleteException (_Unwind_Exception *exception UNUSED)
{
	abort_cxx_exception_unwind (__func__);
}

void *_Unwind_GetLanguageSpecificData (struct _Unwind_Context *context UNUSED)
{
	abort_cxx_exception_unwind (__func__);
}

_Unwind_Ptr _Unwind_GetRegionStart (struct _Unwind_Context *context UNUSED)
{
	abort_cxx_exception_unwind (__func__);
}

_Unwind_Reason_Code _Unwind_RaiseException (_Unwind_Exception *exception UNUSED)
{
	abort_cxx_exception_unwind (__func__);
}

void _Unwind_Resume (_Unwind_Exception *exception UNUSED)
{
	abort_cxx_exception_unwind (__func__);
}

#if defined (__arm__)
_Unwind_Reason_Code __aeabi_unwind_cpp_pr0 (
	_Unwind_State state UNUSED,
	_Unwind_Exception *exception UNUSED,
	struct _Unwind_Context *context UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}

_Unwind_Reason_Code __aeabi_unwind_cpp_pr1 (
	_Unwind_State state UNUSED,
	_Unwind_Exception *exception UNUSED,
	struct _Unwind_Context *context UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}

_Unwind_Reason_Code __aeabi_unwind_cpp_pr2 (
	_Unwind_State state UNUSED,
	_Unwind_Exception *exception UNUSED,
	struct _Unwind_Context *context UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}

_Unwind_Reason_Code __gnu_unwind_frame (
	_Unwind_Exception *exception UNUSED,
	struct _Unwind_Context *context UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}

_Unwind_VRS_Result _Unwind_VRS_Get (
	struct _Unwind_Context *context UNUSED,
	_Unwind_VRS_RegClass regclass UNUSED,
	uint32_t regno UNUSED,
	_Unwind_VRS_DataRepresentation representation UNUSED,
	void *value UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}

_Unwind_VRS_Result _Unwind_VRS_Set (
	struct _Unwind_Context *context UNUSED,
	_Unwind_VRS_RegClass regclass UNUSED,
	uint32_t regno UNUSED,
	_Unwind_VRS_DataRepresentation representation UNUSED,
	void *value UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}
#else
_Unwind_Word _Unwind_GetIP (struct _Unwind_Context *context UNUSED)
{
	abort_cxx_exception_unwind (__func__);
}

void _Unwind_SetGR (
	struct _Unwind_Context *context UNUSED,
	int index UNUSED,
	_Unwind_Word value UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}

void _Unwind_SetIP (
	struct _Unwind_Context *context UNUSED,
	_Unwind_Word value UNUSED
)
{
	abort_cxx_exception_unwind (__func__);
}
#endif

#undef UNUSED

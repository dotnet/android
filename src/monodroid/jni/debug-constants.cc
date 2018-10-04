#include "debug.h"

using namespace xamarin::android;

// These are moved here so that the Windows build works and we don't need to
// #ifdef references to these out in this case. Debugging code generally works
// only on Unix atm
const char Debug::DEBUG_MONO_CONNECT_PROPERTY[]      = "debug.mono.connect";
const char Debug::DEBUG_MONO_DEBUG_PROPERTY[]        = "debug.mono.debug";
const char Debug::DEBUG_MONO_ENV_PROPERTY[]          = "debug.mono.env";
const char Debug::DEBUG_MONO_EXTRA_PROPERTY[]        = "debug.mono.extra";
const char Debug::DEBUG_MONO_GC_PROPERTY[]           = "debug.mono.gc";
const char Debug::DEBUG_MONO_GDB_PROPERTY[]          = "debug.mono.gdb";
const char Debug::DEBUG_MONO_GDBPORT_PROPERTY[]      = "debug.mono.gdbport";
const char Debug::DEBUG_MONO_LOG_PROPERTY[]          = "debug.mono.log";
const char Debug::DEBUG_MONO_MAX_GREFC[]             = "debug.mono.max_grefc";
const char Debug::DEBUG_MONO_PROFILE_PROPERTY[]      = "debug.mono.profile";
const char Debug::DEBUG_MONO_RUNTIME_ARGS_PROPERTY[] = "debug.mono.runtime_args";
const char Debug::DEBUG_MONO_SOFT_BREAKPOINTS[]      = "debug.mono.soft_breakpoints";
const char Debug::DEBUG_MONO_TRACE_PROPERTY[]        = "debug.mono.trace";
const char Debug::DEBUG_MONO_WREF_PROPERTY[]         = "debug.mono.wref";

extern "C" const char *__get_debug_mono_log_property (void)
{
	return static_cast<const char*> (Debug::DEBUG_MONO_LOG_PROPERTY);
}

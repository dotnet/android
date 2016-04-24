#ifndef __MONODROID_DEBUG_H__
#define __MONODROID_DEBUG_H__

/* Android property containing connection information, set by XS */
#define DEBUG_MONO_CONNECT_PROPERTY      "debug.mono.connect"
#define DEBUG_MONO_DEBUG_PROPERTY        "debug.mono.debug"
#define DEBUG_MONO_ENV_PROPERTY          "debug.mono.env"
#define DEBUG_MONO_EXTRA_PROPERTY        "debug.mono.extra"
#define DEBUG_MONO_GC_PROPERTY           "debug.mono.gc"
#define DEBUG_MONO_GDB_PROPERTY          "debug.mono.gdb"
#define DEBUG_MONO_GDBPORT_PROPERTY      "debug.mono.gdbport"
#define DEBUG_MONO_LOG_PROPERTY          "debug.mono.log"
#define DEBUG_MONO_MAX_GREFC             "debug.mono.max_grefc"
#define DEBUG_MONO_PROFILE_PROPERTY      "debug.mono.profile"
#define DEBUG_MONO_RUNTIME_ARGS_PROPERTY "debug.mono.runtime_args"
#define DEBUG_MONO_SOFT_BREAKPOINTS      "debug.mono.soft_breakpoints"
#define DEBUG_MONO_TRACE_PROPERTY        "debug.mono.trace"
#define DEBUG_MONO_WREF_PROPERTY         "debug.mono.wref"

#ifndef WINDOWS
int start_connection (char *options);
#endif

#endif /* __MONODROID_DEBUG_H__ */

// This is a -*- C++ -*- header
#ifndef __MONODROID_DEBUG_H__
#define __MONODROID_DEBUG_H__

#include <stdint.h>
#include <pthread.h>

#ifdef __cplusplus
namespace xamarin { namespace android
{
	class Debug
	{
	private:
		struct ConnOptions
		{
			int64_t timeout_time;
		};

	public:
		/* Android property containing connection information, set by XS */
		static const char DEBUG_MONO_CONNECT_PROPERTY[];
		static const char DEBUG_MONO_DEBUG_PROPERTY[];
		static const char DEBUG_MONO_ENV_PROPERTY[];
		static const char DEBUG_MONO_EXTRA_PROPERTY[];
		static const char DEBUG_MONO_GC_PROPERTY[];
		static const char DEBUG_MONO_GDB_PROPERTY[];
		static const char DEBUG_MONO_GDBPORT_PROPERTY[];
		static const char DEBUG_MONO_LOG_PROPERTY[];
		static const char DEBUG_MONO_MAX_GREFC[];
		static const char DEBUG_MONO_PROFILE_PROPERTY[];
		static const char DEBUG_MONO_RUNTIME_ARGS_PROPERTY[];
		static const char DEBUG_MONO_SOFT_BREAKPOINTS[];
		static const char DEBUG_MONO_TRACE_PROPERTY[];
		static const char DEBUG_MONO_WREF_PROPERTY[];

	public:
		explicit Debug ()
		{}

#if !defined (WINDOWS) && defined (DEBUG)
		int start_connection (char *options);

	private:
		void parse_options (char *options, ConnOptions *opts);
		int process_connection (int fd);
		int handle_server_connection (void);
		friend void* conn_thread (void *arg);

	private:
		static int conn_port;
		static pthread_t conn_thread_id;

#endif
	};
} }
#else // __cplusplus
const char *__get_debug_mono_log_property (void);

#ifndef DEBUG_MONO_LOG_PROPERTY
#define DEBUG_MONO_LOG_PROPERTY __get_debug_mono_log_property ()
#endif

#endif // __cplusplus

#endif /* __MONODROID_DEBUG_H__ */

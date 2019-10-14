//
// debug.c: Debugging/Profiling support code
//
// Copyright 2014 Xamarin Inc.
//
// Based on code from mt's libmonotouch/debug.m file.
//

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifndef WINDOWS
#include <arpa/inet.h>
#include <sys/socket.h>
#include <sys/select.h>
#include <sys/utsname.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#endif

#include <sys/types.h>
#include <sys/time.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <ctype.h>
#include <assert.h>
#include <limits.h>
#include <dlfcn.h>
#include <mono/metadata/mono-debug.h>

#ifdef ANDROID
#include <android/log.h>
#endif

#include "java-interop-util.h"

#include "monodroid.h"
#include "debug.hh"
#include "util.hh"
#include "globals.hh"
#include "cpp-util.hh"

using namespace xamarin::android;

//
// The communication between xs and the app works as follows:
// - the app listens on a port
// - xs connects and sends commands
// - some commands cause the current connection to be passed to a subsystem (debugger/profiler/stdio etc).
//   in that case, the app starts listening again on the port
//
// The protocol is binary, packets look like:
// <cmd length as a byte><cmd>
// i.e. "\x14start profiler: log:"
//
//

namespace xamarin::android
{
	void* conn_thread (void *arg);
}

void
Debug::monodroid_profiler_load (const char *libmono_path, const char *desc, const char *logfile)
{
	const char* col = strchr (desc, ':');
	char *mname_ptr;

	if (col != nullptr) {
		size_t name_len = static_cast<size_t>(col - desc);
		size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, name_len, 1);
		mname_ptr = new char [alloc_size];
		strncpy (mname_ptr, desc, name_len);
		mname_ptr [name_len] = 0;
	} else {
		mname_ptr = utils.strdup_new (desc);
	}
	simple_pointer_guard<char[]> mname (mname_ptr);

	int dlopen_flags = RTLD_LAZY;
	simple_pointer_guard<char[]> libname (utils.string_concat ("libmono-profiler-", mname.get (), ".so"));
	bool found = false;
	void *handle = androidSystem.load_dso_from_any_directories (libname, dlopen_flags);
	found = load_profiler_from_handle (handle, desc, mname);

	if (!found && libmono_path != nullptr) {
		simple_pointer_guard<char[]> full_path (utils.path_combine (libmono_path, libname));
		handle = androidSystem.load_dso (full_path, dlopen_flags, FALSE);
		found = load_profiler_from_handle (handle, desc, mname);
	}

	if (found && logfile != nullptr)
		utils.set_world_accessable (logfile);

	if (!found)
		log_warn (LOG_DEFAULT,
				"The '%s' profiler wasn't found in the main executable nor could it be loaded from '%s'.",
		        mname.get (),
		        libname.get ());
}

/* Profiler support cribbed from mono/metadata/profiler.c */

typedef void (*ProfilerInitializer) (const char*);

bool
Debug::load_profiler (void *handle, const char *desc, const char *symbol)
{
	ProfilerInitializer func = reinterpret_cast<ProfilerInitializer> (dlsym (handle, symbol));
	log_warn (LOG_DEFAULT, "Looking for profiler init symbol '%s'? %p", symbol, func);

	if (func != nullptr) {
		func (desc);
		return true;
	}
	return false;
}

bool
Debug::load_profiler_from_handle (void *dso_handle, const char *desc, const char *name)
{
	if (!dso_handle)
		return false;

	simple_pointer_guard<char[]> symbol (utils.string_concat (INITIALIZER_NAME, "_", name));
	bool result = load_profiler (dso_handle, desc, symbol);

	if (result)
		return true;
	dlclose (dso_handle);
	return false;
}

#if defined (DEBUG) && !defined (WINDOWS)
inline void
Debug::parse_options (char *options, ConnOptions *opts)
{
	char **args, **ptr;

	log_info (LOG_DEFAULT, "Connection options: '%s'", options);

	args = utils.monodroid_strsplit (options, ",", 0);

	for (ptr = args; ptr && *ptr; ptr++) {
		const char *arg = *ptr;

		if (strstr (arg, "port=") == arg) {
			int port = atoi (arg + strlen ("port="));
			if (port < 0 || port > USHRT_MAX) {
				log_error (LOG_DEFAULT, "Invalid debug port value %d", port);
				continue;
			}

			conn_port = static_cast<uint16_t>(port);
			log_info (LOG_DEFAULT, "XS port = %d", conn_port);
		} else if (strstr (arg, "timeout=") == arg) {
			char *endp;

			arg += strlen ("timeout=");
			opts->timeout_time = strtoll (arg, &endp, 10);
			if ((endp == arg) || (*endp != '\0'))
				log_error (LOG_DEFAULT, "Invalid --timeout argument.");
		} else {
			log_info (LOG_DEFAULT, "Unknown connection option: '%s'", arg);
		}
	}
}

/*
 * start_connection:
 *
 *   Handle the communication with XS on startup. Call process_cmd () for each command received from XS.
 * Returns:
 * - 1 on success
 * - 0 on error
 * - 2 if no connection is neccessary
 */
int
Debug::start_connection (char *options)
{
	int res;
	ConnOptions opts;
	int64_t cur_time;

	memset (&opts, 0, sizeof (opts));

	parse_options (options, &opts);

	cur_time = time (nullptr);

	if (opts.timeout_time && cur_time > opts.timeout_time) {
		log_warn (LOG_DEBUGGER, "Not connecting to IDE as the timeout value has been reached; current-time: %lli  timeout: %lli", cur_time, opts.timeout_time);
		return 2;
	}

	if (!conn_port)
		return 0;

	res = pthread_create (&conn_thread_id, nullptr, xamarin::android::conn_thread, this);
	if (res) {
		log_error (LOG_DEFAULT, "Failed to create connection thread: %s", strerror (errno));
		return 1;
	}

	return 0;
}

void
Debug::start_debugging_and_profiling ()
{
	char *connect_args;
	utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_CONNECT_PROPERTY, &connect_args);

	if (connect_args != nullptr) {
		int res = start_connection (connect_args);
		if (res != 2) {
			if (res) {
				log_fatal (LOG_DEBUGGER, "Could not start a connection to the debugger with connection args '%s'.", connect_args);
				exit (FATAL_EXIT_DEBUGGER_CONNECT);
			}

			/* Wait for XS to configure debugging/profiling */
			gettimeofday(&wait_tv, nullptr);
			wait_ts.tv_sec = wait_tv.tv_sec + 2;
			wait_ts.tv_nsec = wait_tv.tv_usec * 1000;
			start_debugging ();
			start_profiling ();
		}
		delete[] connect_args;
	}
}

/*
 * process_connection:
 *
 * Handle communication on the socket FD. Return TRUE if its neccessary to create more connections to handle more data.
 * Call process_cmd () with each command received.
 */
inline bool
Debug::process_connection (int fd)
{
	// make sure the fd/socket blocks on reads/writes
	fcntl (fd, F_SETFL, fcntl (fd, F_GETFL, nullptr) & ~O_NONBLOCK);

	while (true) {
		char command [257];
		uint8_t cmd_len;

		ssize_t rv = utils.recv_uninterrupted (fd, &cmd_len, sizeof(cmd_len));
		if (rv == 0) {
			log_info (LOG_DEFAULT, "EOF on socket.\n");
			return false;
		}
		if (rv <= 0) {
			log_info (LOG_DEFAULT, "Error while receiving command from XS (%s)\n", strerror (errno));
			return false;
		}

		rv = utils.recv_uninterrupted (fd, command, cmd_len);
		if (rv <= 0) {
			log_info (LOG_DEFAULT, "Error while receiving command from XS (%s)\n", strerror (errno));
			return false;
		}

		// null-terminate
		command [cmd_len] = 0;

		log_info (LOG_DEFAULT, "Received cmd: '%s'.", command);

		if (process_cmd (fd, command))
			return true;
	}
}

inline int
Debug::handle_server_connection (void)
{
	int listen_socket = socket (PF_INET, SOCK_STREAM, IPPROTO_TCP);
	if (listen_socket == -1) {
		log_info (LOG_DEFAULT, "Could not create socket for XS to connect to: %s", strerror (errno));
		return 1;
	}

	int flags = 1;
	int rv = setsockopt (listen_socket, SOL_SOCKET, SO_REUSEADDR, &flags, sizeof (flags));
	if (rv == -1 && utils.should_log (LOG_DEFAULT)) {
		log_info_nocheck (LOG_DEFAULT, "Could not set SO_REUSEADDR on the listening socket (%s)", strerror (errno));
		// not a fatal failure
	}

	// Bind
	bool need_new_conn = true;
	uint16_t listen_port = conn_port;
	sockaddr_in listen_addr;
	memset (&listen_addr, 0, sizeof (listen_addr));
	listen_addr.sin_family = AF_INET;
	listen_addr.sin_port = htons (listen_port);
	listen_addr.sin_addr.s_addr = INADDR_ANY;
	rv = bind (listen_socket, (struct sockaddr *) &listen_addr, sizeof (listen_addr));
	if (rv == -1) {
		log_info (LOG_DEFAULT, "Could not bind to address: %s", strerror (errno));
		rv = 2;
		goto cleanup;
	}

	// Make the socket non-blocking
	flags = fcntl (listen_socket, F_GETFL, nullptr);
	flags |= O_NONBLOCK;
	fcntl (listen_socket, F_SETFL, flags);

	rv = listen (listen_socket, 1);
	if (rv == -1) {
		log_info (LOG_DEFAULT, "Could not listen for XS: %s", strerror (errno));
		rv = 2;
		goto cleanup;
	}

	// Wait for connections
	timeval start;
	start.tv_sec = 0;
	start.tv_usec = 0;

	while (need_new_conn) {
		fd_set rset;

		FD_ZERO (&rset);
		FD_SET (listen_socket, &rset);

		do {
			timeval tv;
			timeval now;

			// Calculate how long we can wait if we can only work for 2s since we started
			gettimeofday (&now, nullptr);
			if (start.tv_sec == 0) {
				start.tv_sec = now.tv_sec;
				start.tv_usec = now.tv_usec;
				tv.tv_sec = 2;
				tv.tv_usec = 0;
			} else if ((start.tv_sec + 2 == now.tv_sec && start.tv_usec < now.tv_usec) || start.tv_sec + 2 < now.tv_sec) {
				// timeout
			} else {
				tv.tv_sec = start.tv_sec + 2 - now.tv_sec;
				if (start.tv_usec > now.tv_usec) {
					tv.tv_usec = start.tv_usec - now.tv_usec;
				} else {
					tv.tv_sec--;
					tv.tv_usec = 1000000 + start.tv_usec - now.tv_usec;
				}
			}

			// LOG ("MonoTouch: Waiting for connections from XS, sec: %i usec: %i\n", (int) tv.tv_sec, (int) tv.tv_usec);

			if ((rv = select (listen_socket + 1, &rset, nullptr, nullptr, &tv)) == 0) {
				// timeout hit, no connections available.
				log_info (LOG_DEFAULT, "Listened2 for connections from XS for 2 seconds, nobody connected.\n");
				rv = 3;
				goto cleanup;
			}
		} while (rv == -1 && errno == EINTR);

		if (rv == -1) {
			log_info (LOG_DEFAULT, "Failed while waiting for XS to connect: %s", strerror (errno));
			rv = 2;
			goto cleanup;
		}

		socklen_t len = sizeof (struct sockaddr_in);
		int fd = accept (listen_socket, (struct sockaddr *) &listen_addr, &len);
		if (fd == -1) {
			log_info (LOG_DEFAULT, "Failed to accept connection from XS: %s", strerror (errno));
			rv = 3;
			goto cleanup;
		}

		flags = 1;
		if (setsockopt (fd, IPPROTO_TCP, TCP_NODELAY, (char *) &flags, sizeof (flags)) < 0) {
			log_info (LOG_DEFAULT, "Could not set TCP_NODELAY on socket (%s)", strerror (errno));
			// not a fatal failure
		}

		log_info (LOG_DEFAULT, "Successfully received connection from XS on port %i, fd: %i\n", listen_port, fd);

		need_new_conn = process_connection (fd);
	}

	log_info (LOG_DEFAULT, "Successfully talked to XS. Will continue startup now.\n");

	rv = 0;

cleanup:
	close (listen_socket);
	return rv;
}

/*
 * process_cmd:
 *
 *   Process a command received from XS through a socket connection.
 * This is called on a separate thread.
 * Return true, if a new connection need to be opened.
 */
bool
Debug::process_cmd (int fd, char *cmd)
{
	static constexpr char CONNECT_OUTPUT_CMD[] = "connect output";
	if (strcmp (cmd, CONNECT_OUTPUT_CMD) == 0) {
		dup2 (fd, 1);
		dup2 (fd, 2);
		return true;
	}

	static constexpr char CONNECT_STDOUT_CMD[] = "connect stdout";
	if (strcmp (cmd, CONNECT_STDOUT_CMD) == 0) {
		dup2 (fd, 1);
		return true;
	}

	static constexpr char CONNECT_STDERR_CMD[] = "connect stderr";
	if (strcmp (cmd, CONNECT_STDERR_CMD) == 0) {
		dup2 (fd, 2);
		return true;
	}

	static constexpr char DISCARD_CMD[] = "discard";
	if (strcmp (cmd, DISCARD_CMD) == 0) {
		return true;
	}

	static constexpr char PING_CMD[] = "ping";
	if (strcmp (cmd, PING_CMD) == 0) {
		if (!utils.send_uninterrupted (fd, const_cast<void*> (reinterpret_cast<const void*> ("pong")), 5))
			log_error (LOG_DEFAULT, "Got keepalive request from XS, but could not send response back (%s)\n", strerror (errno));
		return false;
	}

	static constexpr char EXIT_PROCESS_CMD[] = "exit process";
	if (strcmp (cmd, EXIT_PROCESS_CMD) == 0) {
		log_info (LOG_DEFAULT, "Debugger requested an exit, will exit immediately.\n");
		fflush (stdout);
		fflush (stderr);
		exit (0);
	}

	bool use_fd = false;
	static constexpr char START_DEBUGGER_CMD[] = "start debugger: ";
	static constexpr size_t START_DEBUGGER_CMD_LEN = sizeof(START_DEBUGGER_CMD) - 1;
	static constexpr char VALUE_NO[] = "no";
	if (strncmp (cmd, START_DEBUGGER_CMD, START_DEBUGGER_CMD_LEN) == 0) {
		const char *debugger = cmd + START_DEBUGGER_CMD_LEN;

		static constexpr char DEBUGGER_SDB[] = "sdb";
		if (strcmp (debugger, VALUE_NO) == 0) {
			/* disabled */
		} else if (strcmp (debugger, DEBUGGER_SDB) == 0) {
			sdb_fd = fd;
			use_fd = true;
		}
		/* Notify the main thread (start_debugging ()) */
		debugging_configured = true;
		pthread_mutex_lock (&process_cmd_mutex);
		pthread_cond_signal (&process_cmd_cond);
		pthread_mutex_unlock (&process_cmd_mutex);
		return use_fd;
	}

	static constexpr char START_PROFILER_CMD[] = "start profiler: ";
	static constexpr size_t START_PROFILER_CMD_LEN = sizeof(START_PROFILER_CMD) - 1;
	if (strncmp (cmd, START_PROFILER_CMD, START_PROFILER_CMD_LEN) == 0) {
		const char *prof = cmd + START_PROFILER_CMD_LEN;

		static constexpr char PROFILER_LOG[] = "log:";
		static constexpr size_t PROFILER_LOG_LEN = sizeof(PROFILER_LOG) - 1;
		if (strcmp (prof, VALUE_NO) == 0) {
			/* disabled */
		} else if (strncmp (prof, PROFILER_LOG, PROFILER_LOG_LEN) == 0) {
			use_fd = true;
			profiler_fd = fd;
			profiler_description = utils.monodroid_strdup_printf ("%s,output=#%i", prof, profiler_fd);
		} else {
			log_error (LOG_DEFAULT, "Unknown profiler: '%s'", prof);
		}
		/* Notify the main thread (start_profiling ()) */
		profiler_configured = true;
		pthread_mutex_lock (&process_cmd_mutex);
		pthread_cond_signal (&process_cmd_cond);
		pthread_mutex_unlock (&process_cmd_mutex);
		return use_fd;
	} else {
		log_error (LOG_DEFAULT, "Unsupported command: '%s'", cmd);
	}

	return false;
}

#if !defined (WINDOWS)

void
Debug::start_debugging (void)
{
	// wait for debugger configuration to finish
	pthread_mutex_lock (&process_cmd_mutex);
	while (!debugging_configured && !config_timedout) {
		if (pthread_cond_timedwait (&process_cmd_cond, &process_cmd_mutex, &wait_ts) == ETIMEDOUT)
			config_timedout = true;
	}
	pthread_mutex_unlock (&process_cmd_mutex);

	if (sdb_fd == 0)
		return;

	embeddedAssemblies.set_register_debug_symbols (true);

	char *debug_arg = utils.monodroid_strdup_printf ("--debugger-agent=transport=socket-fd,address=%d,embedding=1", sdb_fd);
	char *debug_options[] = {
		debug_arg,
		nullptr
	};

	// this text is used in unit tests to check the debugger started
	// do not change it without updating the test.
	log_warn (LOG_DEBUGGER, "Trying to initialize the debugger with options: %s", debug_arg);

	if (enable_soft_breakpoints ()) {
		constexpr char soft_breakpoints[] = "--soft-breakpoints";
		debug_options[1] = const_cast<char*> (soft_breakpoints);
		mono_jit_parse_options (2, debug_options);
	} else {
		mono_jit_parse_options (1, debug_options);
	}

	mono_debug_init (MONO_DEBUG_FORMAT_MONO);
}

void
Debug::start_profiling ()
{
	// wait for profiler configuration to finish
	pthread_mutex_lock (&process_cmd_mutex);
	while (!profiler_configured && !config_timedout) {
		if (pthread_cond_timedwait (&process_cmd_cond, &process_cmd_mutex, &wait_ts) == ETIMEDOUT)
			config_timedout = TRUE;
	}
	pthread_mutex_unlock (&process_cmd_mutex);

	if (!profiler_description)
		return;

	log_info (LOG_DEFAULT, "Loading profiler: '%s'", profiler_description);
	monodroid_profiler_load (androidSystem.get_runtime_libdir (), profiler_description, nullptr);
}

#endif  // !def WINDOWS

#ifdef ANDROID
#ifdef DEBUG
static const char *soft_breakpoint_kernel_list[] = {
	"2.6.32.21-g1e30168", nullptr
};

bool
Debug::enable_soft_breakpoints (void)
{
	utsname name;

	/* This check is to make debugging work on some old Samsung device which had
	 * a patched kernel that would abort the application after several segfaults
	 * (with the SIGSEGV being used for single-stepping in Mono)
	*/
	uname (&name);
	for (const char** ptr = soft_breakpoint_kernel_list; *ptr; ptr++) {
		if (strcmp (name.release, *ptr) == 0) {
			log_info (LOG_DEBUGGER, "soft breakpoints enabled due to kernel version match (%s)", name.release);
			return 1;
		}
	}

	char *value;
	/* Soft breakpoints are enabled by default */
	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_SOFT_BREAKPOINTS, &value) <= 0) {
		log_info (LOG_DEBUGGER, "soft breakpoints enabled by default (%s property not defined)", Debug::DEBUG_MONO_SOFT_BREAKPOINTS);
		return 1;
	}

	bool ret;
	if (strcmp ("0", value) == 0) {
		ret = false;
		log_info (LOG_DEBUGGER, "soft breakpoints disabled (%s property set to %s)", Debug::DEBUG_MONO_SOFT_BREAKPOINTS, value);
	} else {
		ret = true;
		log_info (LOG_DEBUGGER, "soft breakpoints enabled (%s property set to %s)", Debug::DEBUG_MONO_SOFT_BREAKPOINTS, value);
	}
	delete[] value;
	return ret;
}
#endif /* DEBUG */
#else  /* !defined (ANDROID) */
#if defined (DEBUG) && !defined (WINDOWS)
#ifndef enable_soft_breakpoints
[[maybe_unused]] bool
Debug::enable_soft_breakpoints (void)
{
	return false;
}
#endif /* DEBUG */
#endif // enable_soft_breakpoints
#endif /* defined (ANDROID) */

// TODO: this is less than ideal. We can't use std::function or std::bind beause we
// don't have the C++ stdlib on Android (well, we do but including it would make the
// app huge so we don't want to involve it). To better solve it we need our own equivalent
// to std::function
void*
xamarin::android::conn_thread (void *arg)
{
	assert (arg != nullptr);

	int res;
	Debug *instance = static_cast<Debug*> (arg);
	res = instance->handle_server_connection ();
	if (res && res != 3) {
		log_fatal (LOG_DEBUGGER, "Error communicating with the IDE, exiting...");
		exit (FATAL_EXIT_DEBUGGER_CONNECT);
	}

	return nullptr;
}
#endif /* DEBUG */

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
#include <arpa/inet.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <sys/select.h>
#include <sys/time.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <ctype.h>
#include <assert.h>

#ifdef ANDROID
#include <android/log.h>
#endif

extern "C" {
#include "java-interop-util.h"
}

#include "monodroid.h"
#include "debug.h"
#include "util.h"
#include "globals.h"

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

// monodroid-glue.c
extern int process_cmd (int fd, char *cmd);
namespace xamarin { namespace android
{
	void* conn_thread (void *arg);
}}

#ifdef DEBUG
using namespace xamarin::android;

int Debug::conn_port = 0;
pthread_t Debug::conn_thread_id = 0;

inline void
Debug::parse_options (char *options, ConnOptions *opts)
{
	char **args, **ptr;

	log_info (LOG_DEFAULT, "Connection options: '%s'", options);

	args = utils.monodroid_strsplit (options, ",", -1);

	for (ptr = args; ptr && *ptr; ptr++) {
		const char *arg = *ptr;

		if (strstr (arg, "port=") == arg) {
			conn_port = atoi (arg + strlen ("port="));

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

/*
 * process_connection:
 *
 * Handle communication on the socket FD. Return TRUE if its neccessary to create more connections to handle more data.
 * Call process_cmd () with each command received.
 */
inline int
Debug::process_connection (int fd)
{
	// make sure the fd/socket blocks on reads/writes
	fcntl (fd, F_SETFL, fcntl (fd, F_GETFL, nullptr) & ~O_NONBLOCK);

	while (TRUE) {
		char command [257];
		int rv;
		unsigned char cmd_len;

		rv = utils.recv_uninterrupted (fd, &cmd_len, 1);
		if (rv == 0) {
			log_info (LOG_DEFAULT, "EOF on socket.\n");
			return FALSE;
		}
		if (rv <= 0) {
			log_info (LOG_DEFAULT, "Error while receiving command from XS (%s)\n", strerror (errno));
			return FALSE;
		}

		rv = utils.recv_uninterrupted (fd, command, cmd_len);
		if (rv <= 0) {
			log_info (LOG_DEFAULT, "Error while receiving command from XS (%s)\n", strerror (errno));
			return FALSE;
		}

		// null-terminate
		command [cmd_len] = 0;

		log_info (LOG_DEFAULT, "Received cmd: '%s'.", command);

		rv = process_cmd (fd, command);
		if (rv)
			return TRUE;
	}
}

inline int
Debug::handle_server_connection (void)
{
	int listen_port = conn_port;
	struct sockaddr_in listen_addr;
	int listen_socket;
	socklen_t len;
	fd_set rset;
	struct timeval tv;
	struct timeval start;
	struct timeval now;
	int rv, flags, fd;
	int need_new_conn;

	listen_socket = socket (PF_INET, SOCK_STREAM, IPPROTO_TCP);
	if (listen_socket == -1) {
		log_info (LOG_DEFAULT, "Could not create socket for XS to connect to: %s", strerror (errno));
		return 1;
	}

	flags = 1;
	rv = setsockopt (listen_socket, SOL_SOCKET, SO_REUSEADDR, &flags, sizeof (flags));
	if (rv == -1 && utils.should_log (LOG_DEFAULT)) {
		log_info_nocheck (LOG_DEFAULT, "Could not set SO_REUSEADDR on the listening socket (%s)", strerror (errno));
		// not a fatal failure
	}

	// Bind
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
	start.tv_sec = 0;
	start.tv_usec = 0;
	need_new_conn = TRUE;
	while (need_new_conn) {
		FD_ZERO (&rset);
		FD_SET (listen_socket, &rset);

		do {
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

		len = sizeof (struct sockaddr_in);
		fd = accept (listen_socket, (struct sockaddr *) &listen_addr, &len);
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

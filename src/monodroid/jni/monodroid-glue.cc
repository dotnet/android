#include <stdlib.h>
#include <stdarg.h>
#include <jni.h>
#include <time.h>
#include <stdio.h>
#include <string.h>
#include <strings.h>
#include <ctype.h>
#include <assert.h>
#include <errno.h>
#include <limits.h>

#include <dlfcn.h>
#include <fcntl.h>
#include <unistd.h>
#include <stdint.h>

#include <sys/time.h>
#include <sys/types.h>

#include "mono_android_Runtime.h"

#if defined (DEBUG) && !defined (WINDOWS)
#include <fcntl.h>
#include <arpa/inet.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <errno.h>
#endif

#if defined (LINUX)
#include <sys/syscall.h>
#endif

#ifndef WINDOWS
#include <sys/mman.h>
#include <sys/utsname.h>
#else
#include <windef.h>
#include <winbase.h>
#include <shlobj.h>
#include <objbase.h>
#include <knownfolders.h>
#include <shlwapi.h>
#endif

#include <sys/stat.h>
#include <unistd.h>
#include <dirent.h>
#include <pthread.h>

extern "C" {
#include "java-interop-util.h"
#include "logger.h"
}

#include "monodroid.h"
#include "dylib-mono.h"
#include "util.h"
#include "debug.h"
#include "embedded-assemblies.h"
#include "unzip.h"
#include "ioapi.h"
#include "monodroid-glue.h"
#include "mkbundle-api.h"
#include "monodroid-glue-internal.h"
#include "globals.h"

#ifndef WINDOWS
#include "xamarin_getifaddrs.h"
#endif

using namespace xamarin::android;
using namespace xamarin::android::internal;

// This is below the above because we don't want to modify the header with our internal
// implementation details as it would prevent mkbundle from working
#include "mkbundle-api.h"

// TODO: all of these must be moved to some class
static pthread_mutex_t process_cmd_mutex = PTHREAD_MUTEX_INITIALIZER;
static pthread_cond_t process_cmd_cond = PTHREAD_COND_INITIALIZER;
static int debugging_configured;
static int sdb_fd;
static int profiler_configured;
static int profiler_fd;
static char *profiler_description;
#if DEBUG
static int config_timedout;
static struct timeval wait_tv;
static struct timespec wait_ts;
#endif  // def DEBUG
char *xamarin::android::internal::runtime_libdir;
static MonoMethod* registerType;
/*
 * If set, monodroid will spin in a loop until the debugger breaks the wait by
 * clearing monodroid_gdb_wait.
 */
static int wait_for_gdb;
static volatile int monodroid_gdb_wait = TRUE;
static int android_api_level = 0;

static timing_period jit_time;

#include "config.include"
#include "machine.config.include"

/* Can be called by a native debugger to break the wait on startup */
MONO_API void
monodroid_clear_gdb_wait (void)
{
	monodroid_gdb_wait = FALSE;
}

#ifdef WINDOWS
static const char* get_xamarin_android_msbuild_path (void);
const char *AndroidSystem::SYSTEM_LIB_PATH = get_xamarin_android_msbuild_path();
#endif

/* !DO NOT REMOVE! Used by Mono BCL */
MONO_API int
_monodroid_get_android_api_level (void)
{
	return android_api_level;
}

/* Invoked by System.Core.dll!System.IO.MemoryMappedFiles.MemoryMapImpl.getpagesize */
MONO_API int
monodroid_getpagesize (void)
{
#ifndef WINDOWS
	return getpagesize ();
#else
	SYSTEM_INFO info;
	GetSystemInfo (&info);
	return info.dwPageSize;
#endif
}

/* Invoked by:
    - System.Core.dll!System.TimeZoneInfo.Android.GetDefaultTimeZoneName
    - Mono.Android.dll!Android.Runtime.AndroidEnvironment.GetDefaultTimeZone
*/

MONO_API void
monodroid_free (void *ptr)
{
	free (ptr);
}

MONO_API int
monodroid_get_system_property (const char *name, char **value)
{
	return androidSystem.monodroid_get_system_property (name, value);
}

static char*
get_primary_override_dir (JNIEnv *env, jstring_wrapper &home)
{
	return utils.path_combine (home.get_cstr (), ".__override__");
}

// TODO: these must be moved to some class
char *xamarin::android::internal::primary_override_dir;
char *xamarin::android::internal::external_override_dir;
char *xamarin::android::internal::external_legacy_override_dir;

/* Set of Windows-specific utility/reimplementation of Unix functions */
#ifdef WINDOWS

static char *msbuild_folder_path = nullptr;

static const char*
get_xamarin_android_msbuild_path (void)
{
	const char *suffix = "MSBuild\\Xamarin\\Android";
	char *base_path = nullptr;
	wchar_t *buffer = nullptr;

	if (msbuild_folder_path != nullptr)
		return msbuild_folder_path;

	// Get the base path for 'Program Files' on Windows
	if (!SUCCEEDED (SHGetKnownFolderPath (FOLDERID_ProgramFilesX86, 0, nullptr, &buffer))) {
		if (buffer != nullptr)
			CoTaskMemFree (buffer);
		// returns current directory if a global one couldn't be found
		return ".";
	}

	// Compute the final path
	base_path = utils.utf16_to_utf8 (buffer);
	CoTaskMemFree (buffer);
	msbuild_folder_path = utils.path_combine (base_path, suffix);
	free (base_path);

	return msbuild_folder_path;
}

static int
setenv(const char *name, const char *value, int overwrite)
{
	return androidSystem.setenv (name, value, overwrite);
}
#endif // def WINDOWS

typedef void* (*mono_mkbundle_init_ptr) (void (*)(const MonoBundledAssembly **), void (*)(const char* assembly_name, const char* config_xml),void (*) (int mode));
mono_mkbundle_init_ptr mono_mkbundle_init;

typedef void (*mono_mkbundle_initialize_mono_api_ptr) (const BundleMonoAPI *info);
mono_mkbundle_initialize_mono_api_ptr mono_mkbundle_initialize_mono_api;

// This function could be improved if we somehow marked an apk containing just the bundled app as
// such - perhaps another __XA* environment variable? Would certainly make code faster.
static void
setup_bundled_app (const char *dso_name)
{
	static int dlopen_flags = RTLD_LAZY;
	void *libapp = nullptr;

	if (androidSystem.is_embedded_dso_mode_enabled ()) {
		log_info (LOG_DEFAULT, "bundle app: embedded DSO mode");
		libapp = androidSystem.load_dso_from_any_directories (dso_name, dlopen_flags);
	} else {
		bool needs_free = false;
		log_info (LOG_DEFAULT, "bundle app: normal mode");
		char *bundle_path = androidSystem.get_full_dso_path_on_disk (dso_name, &needs_free);
		log_info (LOG_DEFAULT, "bundle_path == %s", bundle_path ? bundle_path : "<nullptr>");
		if (bundle_path == nullptr)
			return;
		log_info (LOG_BUNDLE, "Attempting to load bundled app from %s", bundle_path);
		libapp = androidSystem.load_dso (bundle_path, dlopen_flags, TRUE);
		free (bundle_path);
	}

	if (libapp == nullptr) {
		log_info (LOG_DEFAULT, "No libapp!");
		if (!androidSystem.is_embedded_dso_mode_enabled ()) {
			log_fatal (LOG_BUNDLE, "bundled app initialization error");
			exit (FATAL_EXIT_CANNOT_LOAD_BUNDLE);
		} else {
			log_info (LOG_BUNDLE, "bundled app not found in the APK, ignoring.");
			return;
		}
	}

	mono_mkbundle_initialize_mono_api = reinterpret_cast<mono_mkbundle_initialize_mono_api_ptr> (dlsym (libapp, "initialize_mono_api"));
	if (!mono_mkbundle_initialize_mono_api)
		log_error (LOG_BUNDLE, "Missing initialize_mono_api in the application");

	mono_mkbundle_init = reinterpret_cast<mono_mkbundle_init_ptr> (dlsym (libapp, "mono_mkbundle_init"));
	if (!mono_mkbundle_init)
		log_error (LOG_BUNDLE, "Missing mono_mkbundle_init in the application");
	log_info (LOG_BUNDLE, "Bundled app loaded: %s", dso_name);
}

typedef struct {
	void *dummy;
} MonoDroidProfiler;

static MonoProfilerHandle profiler_handle;

static jclass     TimeZone_class;

static constexpr bool is_running_on_desktop =
#if ANDROID
	false;
#else
	true;
#endif

MONO_API int
_monodroid_max_gref_get (void)
{
	return androidSystem.get_max_gref_count ();
}

MONO_API int
_monodroid_gref_get (void)
{
	return osBridge.get_gc_gref_count ();
}

MONO_API void
_monodroid_gref_log (const char *message)
{
	osBridge._monodroid_gref_log (message);
}

MONO_API int
_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
	return osBridge._monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_gref_log_delete (handle, type, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_weak_gref_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_weak_gref_delete (handle, type, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_lref_log_new (lrefc, handle, type, threadName, threadId, from, from_writable);
}

MONO_API void
_monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	osBridge._monodroid_lref_log_delete (lrefc, handle, type, threadName, threadId, from, from_writable);
}

JNIEnv*
get_jnienv (void)
{
	return osBridge.ensure_jnienv ();
}

/* The context (mapping to a Mono AppDomain) that is currently selected as the
 * active context from the point of view of Java. We cannot rely on the value
 * of `mono_domain_get` for this as it's stored per-thread and we want to be
 * able to switch our different contexts from different threads.
 */
static int current_context_id = -1;

static void
thread_start (MonoProfiler *prof, uintptr_t tid)
{
	JNIEnv* env;
	int r;
#ifdef PLATFORM_ANDROID
	r = osBridge.get_jvm ()->AttachCurrentThread (&env, nullptr);
#else   // ndef PLATFORM_ANDROID
	r = osBridge.get_jvm ()->AttachCurrentThread ((void**) &env, nullptr);
#endif  // ndef PLATFORM_ANDROID
	if (r != JNI_OK) {
#if DEBUG
		log_fatal (LOG_DEFAULT, "ERROR: Unable to attach current thread to the Java VM!");
		exit (FATAL_EXIT_ATTACH_JVM_FAILED);
#endif
	}
}

static void
thread_end (MonoProfiler *prof, uintptr_t tid)
{
	int r;
	r = osBridge.get_jvm ()->DetachCurrentThread ();
	if (r != JNI_OK) {
#if DEBUG
		/*
		log_fatal (LOG_DEFAULT, "ERROR: Unable to detach current thread from the Java VM!");
		 */
#endif
	}
}

FILE *jit_log;

static void
jit_begin (MonoProfiler *prof, MonoMethod *method)
{
	jit_time.mark_end ();

	if (!jit_log)
		return;

	char* name = monoFunctions.method_full_name (method, 1);

	timing_diff diff (jit_time);
	fprintf (jit_log, "JIT method begin: %s elapsed: %lis:%lu::%lu\n", name, diff.sec, diff.ms, diff.ns);

	free (name);
}

static void
jit_done (MonoProfiler *prof, MonoMethod *method, MonoJitInfo* jinfo)
{
	if (!jit_log)
		return;

	char* name = monoFunctions.method_full_name (method, 1);

	jit_time.mark_end ();

	timing_diff diff (jit_time);
	fprintf (jit_log, "JIT method  done: %s elapsed: %lis:%lu::%lu\n", name, diff.sec, diff.ms, diff.ns);

	free (name);
}

#ifndef RELEASE
MonoAssembly*
open_from_update_dir (MonoAssemblyName *aname, char **assemblies_path, void *user_data)
{
	int fi, oi;
	MonoAssembly *result = nullptr;
	int found = 0;
	const char *culture = reinterpret_cast<const char*> (monoFunctions.assembly_name_get_culture (aname));
	const char *name    = reinterpret_cast<const char*> (monoFunctions.assembly_name_get_name (aname));
	char *pname;

	for (oi = 0; oi < AndroidSystem::MAX_OVERRIDES; ++oi)
		if (androidSystem.get_override_dir (oi) != nullptr && utils.directory_exists (androidSystem.get_override_dir (oi)))
			found = 1;
	if (!found)
		return nullptr;

	if (culture != nullptr && strlen (culture) > 0)
		pname = utils.path_combine (culture, name);
	else
		pname = utils.strdup_new (name);

	static const char *formats[] = {
		"%s" MONODROID_PATH_SEPARATOR "%s",
		"%s" MONODROID_PATH_SEPARATOR "%s.dll",
		"%s" MONODROID_PATH_SEPARATOR "%s.exe",
	};

	for (fi = 0; fi < sizeof (formats)/sizeof (formats [0]) && result == nullptr; ++fi) {
		for (oi = 0; oi < AndroidSystem::MAX_OVERRIDES; ++oi) {
			char *fullpath;
			if (androidSystem.get_override_dir (oi) == nullptr || !utils.directory_exists (androidSystem.get_override_dir (oi)))
				continue;
			fullpath = utils.monodroid_strdup_printf (formats [fi], androidSystem.get_override_dir (oi), pname);
			log_info (LOG_ASSEMBLY, "open_from_update_dir: trying to open assembly: %s\n", fullpath);
			if (utils.file_exists (fullpath))
				result = monoFunctions.assembly_open_full (fullpath, nullptr, 0);
			free (fullpath);
			if (result) {
				// TODO: register .mdb, .pdb file
				break;
			}
		}
	}
	delete[] pname;
	if (result && utils.should_log (LOG_ASSEMBLY)) {
		log_info_nocheck (LOG_ASSEMBLY, "open_from_update_dir: loaded assembly: %p\n", result);
	}
	return result;
}
#endif

bool
should_register_file (const char *filename)
{
#ifndef RELEASE
	for (size_t i = 0; i < AndroidSystem::MAX_OVERRIDES; ++i) {
		const char *odir = androidSystem.get_override_dir (i);
		if (odir == nullptr)
			continue;

		char *p       = utils.path_combine (odir, filename);
		bool  exists  = utils.file_exists (p);
		delete[] p;

		if (exists) {
			log_info (LOG_ASSEMBLY, "should not register '%s' as it exists in the override directory '%s'", filename, odir);
			return !exists;
		}
	}
#endif
	return true;
}

static void
gather_bundled_assemblies (JNIEnv *env, jstring_array_wrapper &runtimeApks, bool register_debug_symbols, int *out_user_assemblies_count)
{
#ifndef RELEASE
	for (size_t i = 0; i < AndroidSystem::MAX_OVERRIDES; ++i) {
		const char *p = androidSystem.get_override_dir (i);
		if (!utils.directory_exists (p))
			continue;
		log_info (LOG_ASSEMBLY, "Loading TypeMaps from %s", p);
		embeddedAssemblies.try_load_typemaps_from_directory (p);
	}
#endif

	int prev_num_assemblies = 0;
	for (int32_t i = runtimeApks.get_length () - 1; i >= 0; --i) {
		size_t           cur_num_assemblies;
		jstring_wrapper &apk_file = runtimeApks [i];

		cur_num_assemblies  = embeddedAssemblies.register_from<should_register_file> (apk_file.get_cstr ());

		if (strstr (apk_file.get_cstr (), "/Mono.Android.DebugRuntime") == nullptr &&
		    strstr (apk_file.get_cstr (), "/Mono.Android.Platform.ApiLevel_") == nullptr)
			*out_user_assemblies_count += (cur_num_assemblies - prev_num_assemblies);
		prev_num_assemblies = cur_num_assemblies;
	}
}

#if defined (DEBUG) && !defined (WINDOWS)
int monodroid_debug_connect (int sock, struct sockaddr_in addr) {
	long flags = 0;
	int res = 0;
	fd_set fds;
	struct timeval tv;
	socklen_t len;
	int val = 0;

	flags = fcntl (sock, F_GETFL, nullptr);
	flags |= O_NONBLOCK;
	fcntl (sock, F_SETFL, flags);

	res = connect (sock, (struct sockaddr *) &addr, sizeof (addr));

	if (res < 0) {
		if (errno == EINPROGRESS) {
			while (1) {
				tv.tv_sec = 2;
				tv.tv_usec = 0;
				FD_ZERO (&fds);
				FD_SET (sock, &fds);

				res = select (sock + 1, 0, &fds, 0, &tv);

				if (res <= 0 && errno != EINTR) return -5;

				len = sizeof (int);

				if (getsockopt (sock, SOL_SOCKET, SO_ERROR, &val, &len) < 0) return -3;

				if (val) return -4;

				break;
			}
		} else {
			return -2;
		}
	}

	flags = fcntl (sock, F_GETFL, nullptr);
	flags &= (~O_NONBLOCK);
	fcntl (sock, F_SETFL, flags);

	return 1;
}

int
monodroid_debug_accept (int sock, struct sockaddr_in addr)
{
	char handshake_msg [128];
	int res, accepted;

	res = bind (sock, (struct sockaddr *) &addr, sizeof (addr));
	if (res < 0)
		return -1;

	res = listen (sock, 1);
	if (res < 0)
		return -2;

	accepted = accept (sock, nullptr, nullptr);
	if (accepted < 0)
		return -3;

	sprintf (handshake_msg, "MonoDroid-Handshake\n");
	do {
		res = send (accepted, handshake_msg, strlen (handshake_msg), 0);
	} while (res == -1 && errno == EINTR);
	if (res < 0)
		return -4;

	return accepted;
}
#endif

JNIEXPORT jint JNICALL
JNI_OnLoad (JavaVM *vm, void *reserved)
{
	JNIEnv *env;

	androidSystem.init_max_gref_count ();

	vm->GetEnv ((void**)&env, JNI_VERSION_1_6);
	osBridge.initialize_on_onload (vm, env);

	return JNI_VERSION_1_6;
}

static void
parse_gdb_options (void)
{
	char *val;

	if (!(utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_GDB_PROPERTY, &val) > 0))
		return;

	if (strstr (val, "wait:") == val) {
		/*
		 * The form of the property should be: 'wait:<timestamp>', where <timestamp> should be
		 * the output of date +%s in the android shell.
		 * If this property is set, wait for a native debugger to attach by spinning in a loop.
		 * The debugger can break the wait by setting 'monodroid_gdb_wait' to 0.
		 * If the current time is later than <timestamp> + 10s, the property is ignored.
		 */
		long long v;
		int do_wait = TRUE;

		v = atoll (val + strlen ("wait:"));
		if (v > 100000) {
			time_t secs = time (nullptr);

			if (v + 10 < secs) {
				log_warn (LOG_DEFAULT, "Found stale %s property with value '%s', not waiting.", Debug::DEBUG_MONO_GDB_PROPERTY, val);
				do_wait = FALSE;
			}
		}

		wait_for_gdb = do_wait;
	}

	delete[] val;
}

#if DEBUG
typedef struct {
	int debug;
	int loglevel;
	int64_t timeout_time;
	char *host;
	int sdb_port, out_port;
	mono_bool server;
} RuntimeOptions;

static int
parse_runtime_args (char *runtime_args, RuntimeOptions *options)
{
	char **args, **ptr;

	if (!runtime_args)
		return 1;

	options->timeout_time = 0;

	args = utils.monodroid_strsplit (runtime_args, ",", -1);

	for (ptr = args; ptr && *ptr; ptr++) {
		const char *arg = *ptr;

		if (!strncmp (arg, "debug", 5)) {
			char *host = nullptr;
			int sdb_port = 1000, out_port = -1;

			options->debug = 1;

			if (arg[5] == '=') {
				const char *sep, *endp;

				arg += 6;
				sep = strchr (arg, ':');
				if (sep) {
					host = new char [sep-arg+1];
					memset (host, 0x00, sep-arg+1);
					strncpy (host, arg, sep-arg);
					arg = sep+1;

					sdb_port = (int) strtol (arg, const_cast<char**> (&endp), 10);
					if (endp == arg) {
						log_error (LOG_DEFAULT, "Invalid --debug argument.");
						continue;
					} else if (*endp == ':') {
						arg = endp+1;
						out_port = (int) strtol (arg, const_cast<char**> (&endp), 10);
						if ((endp == arg) || (*endp != '\0')) {
							log_error (LOG_DEFAULT, "Invalid --debug argument.");
							continue;
						}
					} else if (*endp != '\0') {
						log_error (LOG_DEFAULT, "Invalid --debug argument.");
						continue;
					}
				}
			} else if (arg[5] != '\0') {
				log_error (LOG_DEFAULT, "Invalid --debug argument.");
				continue;
			}

			if (!host)
				host = utils.strdup_new ("10.0.2.2");

			options->host = host;
			options->sdb_port = sdb_port;
			options->out_port = out_port;
		} else if (!strncmp (arg, "timeout=", 8)) {
			char *endp;

			arg += sizeof ("timeout");
			options->timeout_time = strtoll (arg, &endp, 10);
			if ((endp == arg) || (*endp != '\0'))
				log_error (LOG_DEFAULT, "Invalid --timeout argument.");
			continue;
		} else if (!strncmp (arg, "server=", 7)) {
			arg += sizeof ("server");
			options->server = *arg == 'y' || *arg == 'Y';
			continue;
		} else if (!strncmp (arg, "loglevel=", 9)) {
			char *endp;

			arg += sizeof ("loglevel");
			options->loglevel = strtoll (arg, &endp, 10);
			if ((endp == arg) || (*endp != '\0'))
				log_error (LOG_DEFAULT, "Invalid --loglevel argument.");
		} else {
				log_error (LOG_DEFAULT, "Unknown runtime argument: '%s'", arg);
			continue;
		}
	}

	utils.monodroid_strfreev (args);
	return 1;
}
#endif  // def DEBUG

static void
set_debug_options (void)
{
	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_DEBUG_PROPERTY, nullptr) == 0)
		return;

	embeddedAssemblies.set_register_debug_symbols (true);
	monoFunctions.debug_init (MONO_DEBUG_FORMAT_MONO);
}

#ifdef ANDROID
#ifdef DEBUG
static const char *soft_breakpoint_kernel_list[] = {
	"2.6.32.21-g1e30168", nullptr
};

static int
enable_soft_breakpoints (void)
{
	struct utsname name;

	/* This check is to make debugging work on some old Samsung device which had
	 * a patched kernel that would abort the application after several segfaults
	 * (with the SIGSEGV being used for single-stepping in Mono)
	*/
	uname (&name);
	for (const char** ptr = soft_breakpoint_kernel_list; *ptr; ptr++) {
		if (!strcmp (name.release, *ptr)) {
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

	int ret;
	if (strcmp ("0", value) == 0) {
		ret = 0;
		log_info (LOG_DEBUGGER, "soft breakpoints disabled (%s property set to %s)", Debug::DEBUG_MONO_SOFT_BREAKPOINTS, value);
	} else {
		ret = 1;
		log_info (LOG_DEBUGGER, "soft breakpoints enabled (%s property set to %s)", Debug::DEBUG_MONO_SOFT_BREAKPOINTS, value);
	}
	delete[] value;
	return 1;
}
#endif /* DEBUG */
#else  /* !defined (ANDROID) */
#ifdef DEBUG
#ifndef enable_soft_breakpoints
static int
enable_soft_breakpoints (void)
{
	return 0;
}
#endif /* DEBUG */
#endif // enable_soft_breakpoints
#endif /* defined (ANDROID) */

static void
mono_runtime_init (char *runtime_args)
{
#if DEBUG
	RuntimeOptions options;
	int64_t cur_time;
#endif
	char *prop_val;

#if defined (DEBUG) && !defined (WINDOWS)
	memset(&options, 0, sizeof (options));

	cur_time = time (nullptr);

	if (!parse_runtime_args (runtime_args, &options)) {
		log_error (LOG_DEFAULT, "Failed to parse runtime args: '%s'", runtime_args);
	} else if (options.debug && cur_time > options.timeout_time) {
		log_warn (LOG_DEBUGGER, "Not starting the debugger as the timeout value has been reached; current-time: %lli  timeout: %lli", cur_time, options.timeout_time);
	} else if (options.debug && cur_time <= options.timeout_time) {
		char *debug_arg;
		char *debug_options [2];

		embeddedAssemblies.set_register_debug_symbols (true);

		debug_arg = utils.monodroid_strdup_printf ("--debugger-agent=transport=dt_socket,loglevel=%d,address=%s:%d,%sembedding=1", options.loglevel, options.host, options.sdb_port,
				options.server ? "server=y," : "");
		debug_options[0] = debug_arg;

		log_warn (LOG_DEBUGGER, "Trying to initialize the debugger with options: %s", debug_arg);

		if (options.out_port > 0) {
			struct sockaddr_in addr;
			int sock;
			int r;

			sock = socket (PF_INET, SOCK_STREAM, IPPROTO_TCP);
			if (sock < 0) {
				log_fatal (LOG_DEBUGGER, "Could not construct a socket for stdout and stderr; does your app have the android.permission.INTERNET permission? %s", strerror (errno));
				exit (FATAL_EXIT_DEBUGGER_CONNECT);
			}

			memset(&addr, 0, sizeof (addr));

			addr.sin_family = AF_INET;
			addr.sin_port = htons (options.out_port);

			if ((r = inet_pton (AF_INET, options.host, &addr.sin_addr)) != 1) {
				log_error (LOG_DEBUGGER, "Could not setup a socket for stdout and stderr: %s",
						r == -1 ? strerror (errno) : "address not parseable in the specified address family");
				exit (FATAL_EXIT_DEBUGGER_CONNECT);
			}

			if (options.server) {
				int accepted = monodroid_debug_accept (sock, addr);
				log_warn (LOG_DEBUGGER, "Accepted stdout connection: %d", accepted);
				if (accepted < 0) {
					log_fatal (LOG_DEBUGGER, "Error accepting stdout and stderr (%s:%d): %s",
							     options.host, options.out_port, strerror (errno));
					exit (FATAL_EXIT_DEBUGGER_CONNECT);
				}

				dup2 (accepted, 1);
				dup2 (accepted, 2);
			} else {
				if (monodroid_debug_connect (sock, addr) != 1) {
					log_fatal (LOG_DEBUGGER, "Error connecting stdout and stderr (%s:%d): %s",
							     options.host, options.out_port, strerror (errno));
					exit (FATAL_EXIT_DEBUGGER_CONNECT);
				}

				dup2 (sock, 1);
				dup2 (sock, 2);
			}
		}

		if (enable_soft_breakpoints ()) {
			constexpr char soft_breakpoints[] = "--soft-breakpoints";
			debug_options[1] = const_cast<char*> (soft_breakpoints);
			monoFunctions.jit_parse_options (2, debug_options);
		} else {
			monoFunctions.jit_parse_options (1, debug_options);
		}

		monoFunctions.debug_init (MONO_DEBUG_FORMAT_MONO);
	} else {
		set_debug_options ();
	}
#else
	set_debug_options ();
#endif

	bool log_methods = utils.should_log (LOG_TIMING) && !(log_timing_categories & LOG_TIMING_BARE);
	if (XA_UNLIKELY (log_methods)) {
		char *jit_log_path = utils.path_combine (androidSystem.get_override_dir (0), "methods.txt");
		jit_log = utils.monodroid_fopen (jit_log_path, "a");
		utils.set_world_accessable (jit_log_path);
		delete[] jit_log_path;
	}

	profiler_handle = monoFunctions.profiler_create ();
	monoFunctions.profiler_install_thread (profiler_handle, thread_start, thread_end);
	if (XA_UNLIKELY (log_methods)) {
		jit_time.mark_start ();
		monoFunctions.profiler_set_jit_begin_callback (profiler_handle, jit_begin);
		monoFunctions.profiler_set_jit_done_callback (profiler_handle, jit_done);
	}

	parse_gdb_options ();

	if (wait_for_gdb) {
		log_warn (LOG_DEFAULT, "Waiting for gdb to attach...");
		while (monodroid_gdb_wait) {
			sleep (1);
		}
	}

	/* Additional runtime arguments passed to mono_jit_parse_options () */
	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_RUNTIME_ARGS_PROPERTY, &prop_val) > 0) {
		char **args, **ptr;
		int argc;

		log_warn (LOG_DEBUGGER, "passing '%s' as extra arguments to the runtime.\n", prop_val);

		args = utils.monodroid_strsplit (prop_val, " ", -1);
		argc = 0;
		delete[] prop_val;

		for (ptr = args; *ptr; ptr++)
			argc ++;

		monoFunctions.jit_parse_options (argc, args);
	}

	monoFunctions.set_signal_chaining (1);
	monoFunctions.set_crash_chaining (1);

	osBridge.register_gc_hooks ();

	if (mono_mkbundle_initialize_mono_api) {
		BundleMonoAPI bundle_mono_api = {
			.mono_register_bundled_assemblies = monoFunctions.get_register_bundled_assemblies_ptr (),
			.mono_register_config_for_assembly = monoFunctions.get_register_config_for_assembly_ptr (),
			.mono_jit_set_aot_mode = reinterpret_cast<void (*)(int)>(monoFunctions.get_jit_set_aot_mode_ptr ()),
			.mono_aot_register_module = monoFunctions.get_aot_register_module_ptr (),
			.mono_config_parse_memory = monoFunctions.get_config_parse_memory_ptr (),
			.mono_register_machine_config = reinterpret_cast<void (*)(const char *)>(monoFunctions.get_register_machine_config_ptr ()),
		};

		/* The initialization function copies the struct */
		mono_mkbundle_initialize_mono_api (&bundle_mono_api);
	}

	if (mono_mkbundle_init)
		mono_mkbundle_init (monoFunctions.get_register_bundled_assemblies_ptr (), monoFunctions.get_register_config_for_assembly_ptr (), reinterpret_cast<void (*)(int)>(monoFunctions.get_jit_set_aot_mode_ptr ()));

	/*
	 * Assembly preload hooks are invoked in _reverse_ registration order.
	 * Looking for assemblies from the update dir takes precedence over
	 * everything else, and thus must go LAST.
	 */
	embeddedAssemblies.install_preload_hooks ();
#ifndef RELEASE
	monoFunctions.install_assembly_preload_hook (open_from_update_dir, nullptr);
#endif
}

static MonoDomain*
create_domain (JNIEnv *env, jclass runtimeClass, jstring_array_wrapper &runtimeApks, jobject loader, bool is_root_domain)
{
	MonoDomain *domain;
	int user_assemblies_count   = 0;;

	gather_bundled_assemblies (env, runtimeApks, embeddedAssemblies.get_register_debug_symbols (), &user_assemblies_count);

	if (!mono_mkbundle_init && user_assemblies_count == 0 && androidSystem.count_override_assemblies () == 0) {
		log_fatal (LOG_DEFAULT, "No assemblies found in '%s' or '%s'. Assuming this is part of Fast Deployment. Exiting...",
		           androidSystem.get_override_dir (0),
		           (AndroidSystem::MAX_OVERRIDES > 1 && androidSystem.get_override_dir (1) != nullptr) ? androidSystem.get_override_dir (1) : "<unavailable>");
		exit (FATAL_EXIT_NO_ASSEMBLIES);
	}

	if (is_root_domain) {
		domain = monoFunctions.jit_init_version (const_cast<char*> ("RootDomain"), const_cast<char*> ("mobile"));
	} else {
		MonoDomain* root_domain = monoFunctions.get_root_domain ();
		char *domain_name = utils.monodroid_strdup_printf ("MonoAndroidDomain%d", android_api_level);
		domain = utils.monodroid_create_appdomain (root_domain, domain_name, /*shadow_copy:*/ 1, /*shadow_directory:*/ androidSystem.get_override_dir (0));
		free (domain_name);
	}

	if (is_running_on_desktop && is_root_domain) {
		// Check that our corlib is coherent with the version of Mono we loaded otherwise
		// tell the IDE that the project likely need to be recompiled.
		char* corlib_error_message = monoFunctions.check_corlib_version ();
		if (corlib_error_message == nullptr) {
			if (!androidSystem.monodroid_get_system_property ("xamarin.studio.fakefaultycorliberrormessage", &corlib_error_message)) {
				free (corlib_error_message);
				corlib_error_message = nullptr;
			}
		}
		if (corlib_error_message != nullptr) {
			jclass ex_klass = env->FindClass ("mono/android/MonoRuntimeException");
			env->ThrowNew (ex_klass, corlib_error_message);
			free (corlib_error_message);
			return nullptr;
		}

		// Load a basic environment for the RootDomain if run on desktop so that we can unload
		// and reload most assemblies including Mono.Android itself
		MonoAssemblyName *aname = monoFunctions.assembly_name_new ("System");
		monoFunctions.assembly_load_full (aname, nullptr, nullptr, 0);
		monoFunctions.assembly_name_free (aname);
	}

	return domain;
}

static jclass System;
static jmethodID System_identityHashCode;

static int
LocalRefsAreIndirect (JNIEnv *env, jclass runtimeClass, int version)
{
	if (version < 14)
		return 0;

	System = utils.get_class_from_runtime_field(env, runtimeClass, "java_lang_System", true);
	System_identityHashCode = env->GetStaticMethodID (System, "identityHashCode", "(Ljava/lang/Object;)I");

	return 1;
}

MONO_API void*
_monodroid_get_identity_hash_code (JNIEnv *env, void *v)
{
	intptr_t rv = env->CallStaticIntMethod (System, System_identityHashCode, v);
	return (void*) rv;
}

MONO_API void*
_monodroid_timezone_get_default_id (void)
{
	JNIEnv *env          = osBridge.ensure_jnienv ();
	jmethodID getDefault = env->GetStaticMethodID (TimeZone_class, "getDefault", "()Ljava/util/TimeZone;");
	jmethodID getID      = env->GetMethodID (TimeZone_class, "getID",      "()Ljava/lang/String;");
	jobject d            = env->CallStaticObjectMethod (TimeZone_class, getDefault);
	jstring id           = reinterpret_cast<jstring> (env->CallObjectMethod (d, getID));
	const char *mutf8    = env->GetStringUTFChars (id, nullptr);
	char *def_id         = strdup (mutf8);

	env->ReleaseStringUTFChars (id, mutf8);
	env->DeleteLocalRef (id);
	env->DeleteLocalRef (d);

	return def_id;
}

MONO_API void
_monodroid_gc_wait_for_bridge_processing (void)
{
	monoFunctions.gc_wait_for_bridge_processing ();
}

struct JnienvInitializeArgs {
	JavaVM         *javaVm;
	JNIEnv         *env;
	jobject         grefLoader;
	jmethodID       Loader_loadClass;
	jclass          grefClass;
	jmethodID       Class_forName;
	unsigned int    logCategories;
	jmethodID       Class_getName;
	int             version;
	int             androidSdkVersion;
	int             localRefsAreIndirect;
	int             grefGcThreshold;
	jobject         grefIGCUserPeer;
	int             isRunningOnDesktop;
};

#define DEFAULT_X_DPI 120.0f
#define DEFAULT_Y_DPI 120.0f

static MonoMethod *runtime_GetDisplayDPI;

/* !DO NOT REMOVE! Used by libgdiplus.so */
MONO_API int
_monodroid_get_display_dpi (float *x_dpi, float *y_dpi)
{
	void *args[2];
	MonoObject *exc = nullptr;

	if (!x_dpi) {
		log_error (LOG_DEFAULT, "Internal error: x_dpi parameter missing in get_display_dpi");
		return -1;
	}

	if (!y_dpi) {
		log_error (LOG_DEFAULT, "Internal error: y_dpi parameter missing in get_display_dpi");
		return -1;
	}

	MonoDomain *domain = nullptr;
	if (!runtime_GetDisplayDPI) {
		domain = monoFunctions.get_root_domain ();
		MonoAssembly *assm = utils.monodroid_load_assembly (domain, "Mono.Android");;

		MonoImage *image = nullptr;
		if (assm != nullptr)
			image = monoFunctions.assembly_get_image  (assm);

		MonoClass *environment = nullptr;
		if (image != nullptr)
			environment = utils.monodroid_get_class_from_image (domain, image, "Android.Runtime", "AndroidEnvironment");

		if (environment != nullptr)
			runtime_GetDisplayDPI = monoFunctions.class_get_method_from_name (environment, "GetDisplayDPI", 2);
	}

	if (!runtime_GetDisplayDPI) {
		*x_dpi = DEFAULT_X_DPI;
		*y_dpi = DEFAULT_Y_DPI;
		return 0;
	}

	args [0] = x_dpi;
	args [1] = y_dpi;
	utils.monodroid_runtime_invoke (domain != nullptr ? domain : monoFunctions.get_root_domain (), runtime_GetDisplayDPI, nullptr, args, &exc);
	if (exc) {
		*x_dpi = DEFAULT_X_DPI;
		*y_dpi = DEFAULT_Y_DPI;
	}

	return 0;
}

static void
lookup_bridge_info (MonoDomain *domain, MonoImage *image, const OSBridge::MonoJavaGCBridgeType *type, OSBridge::MonoJavaGCBridgeInfo *info)
{
	info->klass             = utils.monodroid_get_class_from_image (domain, image, type->_namespace, type->_typename);
	info->handle            = monoFunctions.class_get_field_from_name (info->klass, const_cast<char*> ("handle"));
	info->handle_type       = monoFunctions.class_get_field_from_name (info->klass, const_cast<char*> ("handle_type"));
	info->refs_added        = monoFunctions.class_get_field_from_name (info->klass, const_cast<char*> ("refs_added"));
	info->weak_handle       = monoFunctions.class_get_field_from_name (info->klass, const_cast<char*> ("weak_handle"));
}

static void
init_android_runtime (MonoDomain *domain, JNIEnv *env, jclass runtimeClass, jobject loader)
{
	MonoAssembly *assm;
	MonoClass *runtime;
	MonoImage *image;
	MonoMethod *method;
	jclass lrefLoaderClass;
	int i;

	struct JnienvInitializeArgs init    = {};
	void *args [1];
	args [0] = &init;

	init.javaVm                 = osBridge.get_jvm ();
	init.env                    = env;
	init.logCategories          = log_categories;
	init.version                = env->GetVersion ();
	init.androidSdkVersion      = android_api_level;
	init.localRefsAreIndirect   = LocalRefsAreIndirect (env, runtimeClass, init.androidSdkVersion);
	init.isRunningOnDesktop     = is_running_on_desktop;

	// GC threshold is 90% of the max GREF count
	init.grefGcThreshold        = androidSystem.get_gref_gc_threshold ();

	log_warn (LOG_GC, "GREF GC Threshold: %i", init.grefGcThreshold);

	init.grefClass = utils.get_class_from_runtime_field (env, runtimeClass, "java_lang_Class", true);
	init.Class_getName  = env->GetMethodID (init.grefClass, "getName", "()Ljava/lang/String;");
	init.Class_forName = env->GetStaticMethodID (init.grefClass, "forName", "(Ljava/lang/String;ZLjava/lang/ClassLoader;)Ljava/lang/Class;");

	assm  = utils.monodroid_load_assembly (domain, "Mono.Android");
	image = monoFunctions.assembly_get_image  (assm);

	for (i = 0; i < OSBridge::NUM_GC_BRIDGE_TYPES; ++i) {
		lookup_bridge_info (domain, image, &osBridge.get_java_gc_bridge_type (i), &osBridge.get_java_gc_bridge_info (i));
	}

	runtime                             = utils.monodroid_get_class_from_image (domain, image, "Android.Runtime", "JNIEnv");
	method                              = monoFunctions.class_get_method_from_name (runtime, "Initialize", 1);

	if (method == 0) {
		log_fatal (LOG_DEFAULT, "INTERNAL ERROR: Unable to find Android.Runtime.JNIEnv.Initialize!");
		exit (FATAL_EXIT_MISSING_INIT);
	}
	/* If running on desktop, we may be swapping in a new Mono.Android image when calling this
	 * so always make sure we have the freshest handle to the method.
	 */
	if (registerType == 0 || is_running_on_desktop) {
		registerType = monoFunctions.class_get_method_from_name (runtime, "RegisterJniNatives", 5);
	}
	if (registerType == 0) {
		log_fatal (LOG_DEFAULT, "INTERNAL ERROR: Unable to find Android.Runtime.JNIEnv.RegisterJniNatives!");
		exit (FATAL_EXIT_CANNOT_FIND_JNIENV);
	}
	MonoClass *android_runtime_jnienv = runtime;
	MonoClassField *bridge_processing_field = monoFunctions.class_get_field_from_name (runtime, const_cast<char*> ("BridgeProcessing"));
	if (!android_runtime_jnienv || !bridge_processing_field) {
		log_fatal (LOG_DEFAULT, "INTERNAL_ERROR: Unable to find Android.Runtime.JNIEnv.BridgeProcessing");
		exit (FATAL_EXIT_CANNOT_FIND_JNIENV);
	}

	lrefLoaderClass = env->GetObjectClass (loader);
	init.Loader_loadClass = env->GetMethodID (lrefLoaderClass, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
	env->DeleteLocalRef (lrefLoaderClass);

	init.grefLoader = env->NewGlobalRef (loader);

	init.grefIGCUserPeer  = utils.get_class_from_runtime_field(env, runtimeClass, "mono_android_IGCUserPeer", true);

	osBridge.initialize_on_runtime_init (env, runtimeClass);

	log_info (LOG_DEFAULT, "Calling into managed runtime init");

	timing_period partial_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING)))
		partial_time.mark_start ();

	utils.monodroid_runtime_invoke (domain, method, nullptr, args, nullptr);

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		partial_time.mark_end ();

		timing_diff diff (partial_time);
		log_info_nocheck (LOG_TIMING, "Runtime.init: end native-to-managed transition; elapsed: %lis:%lu::%lu", diff.sec, diff.ms, diff.ns);
	}
}

static MonoClass*
get_android_runtime_class (MonoDomain *domain)
{
	MonoAssembly *assm = utils.monodroid_load_assembly (domain, "Mono.Android");
	MonoImage *image   = monoFunctions.assembly_get_image (assm);
	MonoClass *runtime = utils.monodroid_get_class_from_image (domain, image, "Android.Runtime", "JNIEnv");

	return runtime;
}

static void
shutdown_android_runtime (MonoDomain *domain)
{
	MonoClass *runtime = get_android_runtime_class (domain);
	MonoMethod *method = monoFunctions.class_get_method_from_name (runtime, "Exit", 0);

	utils.monodroid_runtime_invoke (domain, method, nullptr, nullptr, nullptr);
}

static void
propagate_uncaught_exception (MonoDomain *domain, JNIEnv *env, jobject javaThread, jthrowable javaException)
{
	void *args[3];
	MonoClass *runtime = get_android_runtime_class (domain);
	MonoMethod *method = monoFunctions.class_get_method_from_name (runtime, "PropagateUncaughtException", 3);

	args[0] = &env;
	args[1] = &javaThread;
	args[2] = &javaException;
	utils.monodroid_runtime_invoke (domain, method, nullptr, args, nullptr);
}

#if DEBUG
static void
setup_gc_logging (void)
{
	gc_spew_enabled = utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_GC_PROPERTY, nullptr) > 0;
	if (gc_spew_enabled) {
		log_categories |= LOG_GC;
	}
}
#endif

static int
convert_dl_flags (int flags)
{
	int lflags = flags & static_cast<int> (MonoDlKind::MONO_DL_LOCAL) ? 0: RTLD_GLOBAL;

	if (flags & static_cast<int> (MonoDlKind::MONO_DL_LAZY))
		lflags |= RTLD_LAZY;
	else
		lflags |= RTLD_NOW;
	return lflags;
}

static void*
monodroid_dlopen (const char *name, int flags, char **err, void *user_data)
{
	int dl_flags = convert_dl_flags (flags);
	void *h = nullptr;
	char *full_name = nullptr;
	char *basename = nullptr;
	mono_bool libmonodroid_fallback = FALSE;

	/* name is nullptr when we're P/Invoking __Internal, so remap to libmonodroid */
	if (name == nullptr) {
		name = "libmonodroid.so";
		libmonodroid_fallback = TRUE;
	}

	h = androidSystem.load_dso_from_any_directories (name, dl_flags);
	if (h != nullptr) {
		goto done_and_out;
	}

	if (libmonodroid_fallback) {
		full_name = utils.path_combine (AndroidSystem::SYSTEM_LIB_PATH, "libmonodroid.so");
		h = androidSystem.load_dso (full_name, dl_flags, FALSE);
		goto done_and_out;
	}

	if (!strstr (name, ".dll.so") && !strstr (name, ".exe.so")) {
		goto done_and_out;
	}

	basename = const_cast<char*> (strrchr (name, '/'));
	if (basename != nullptr)
		basename++;
	else
		basename = (char*)name;

#if WINDOWS
	basename = utils.monodroid_strdup_printf ("libaot-%s", basename);
#else
	basename = utils.string_concat ("libaot-", basename);
#endif
	h = androidSystem.load_dso_from_any_directories (basename, dl_flags);

	if (h != nullptr && XA_UNLIKELY (utils.should_log (LOG_ASSEMBLY)))
		log_info_nocheck (LOG_ASSEMBLY, "Loaded AOT image '%s'", basename);

  done_and_out:
	if (!h && err) {
		*err = utils.monodroid_strdup_printf ("Could not load library: Library '%s' not found.", full_name);
	}
#if WINDOWS
	free (basename);
#else
	delete[] basename;
#endif
	delete[] full_name;

	return h;
}

static void*
monodroid_dlsym (void *handle, const char *name, char **err, void *user_data)
{
	void *s;

	s = dlsym (handle, name);

	if (!s && err) {
		*err = utils.monodroid_strdup_printf ("Could not find symbol '%s'.", name);
	}

	return s;
}

static void
set_environment_variable_for_directory (JNIEnv *env, const char *name, jstring_wrapper &value, bool createDirectory, int mode )
{
	if (createDirectory) {
		int rv = utils.create_directory (value.get_cstr (), mode);
		if (rv < 0 && errno != EEXIST)
			log_warn (LOG_DEFAULT, "Failed to create directory for environment variable %s. %s", name, strerror (errno));
	}
	setenv (name, value.get_cstr (), 1);
}

static void
set_environment_variable_for_directory (JNIEnv *env, const char *name, jstring_wrapper &value)
{
	set_environment_variable_for_directory (env, name, value, true, DEFAULT_DIRECTORY_MODE);
}

static void
set_environment_variable (JNIEnv *env, const char *name, jstring_wrapper &value)
{
	set_environment_variable_for_directory (env, name, value, false, 0);
}

static void
create_xdg_directory (jstring_wrapper& home, const char *relativePath, const char *environmentVariableName)
{
	char *dir = utils.path_combine (home.get_cstr (), relativePath);
	log_info (LOG_DEFAULT, "Creating XDG directory: %s", dir);
	int rv = utils.create_directory (dir, DEFAULT_DIRECTORY_MODE);
	if (rv < 0 && errno != EEXIST)
		log_warn (LOG_DEFAULT, "Failed to create XDG directory %s. %s", dir, strerror (errno));
	if (environmentVariableName)
		setenv (environmentVariableName, dir, 1);
	delete[] dir;
}

static void
create_xdg_directories_and_environment (JNIEnv *env, jstring_wrapper &homeDir)
{
	create_xdg_directory (homeDir, ".local/share", "XDG_DATA_HOME");
	create_xdg_directory (homeDir, ".config", "XDG_CONFIG_HOME");
}

#if DEBUG
static void
set_debug_env_vars (void)
{
	char *value;

	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_ENV_PROPERTY, &value) == 0)
		return;

	char **args = utils.monodroid_strsplit (value, "|", -1);
	delete[] value;

	for (char **ptr = args; ptr && *ptr; ptr++) {
		char *arg = *ptr;
		char *v = strchr (arg, '=');
		if (v) {
			*v = '\0';
			++v;
		} else {
			constexpr char one[] = "1";
			v = const_cast<char*> (one);
		}
		setenv (arg, v, 1);
		log_info (LOG_DEFAULT, "Env variable '%s' set to '%s'.", arg, v);
	}
	utils.monodroid_strfreev (args);
}
#endif /* DEBUG */

static void
set_trace_options (void)
{
	char *value;

	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_TRACE_PROPERTY, &value) == 0)
		return;

	monoFunctions.jit_set_trace_options (value);
	delete[] value;
}

/* Profiler support cribbed from mono/metadata/profiler.c */

typedef void (*ProfilerInitializer) (const char*);
static constexpr char INITIALIZER_NAME[] = "mono_profiler_init";

static mono_bool
load_profiler (void *handle, const char *desc, const char *symbol)
{
	ProfilerInitializer func = reinterpret_cast<ProfilerInitializer> (dlsym (handle, symbol));
	log_warn (LOG_DEFAULT, "Looking for profiler init symbol '%s'? %p", symbol, func);

	if (func) {
		func (desc);
		return 1;
	}
	return 0;
}

static mono_bool
load_profiler_from_handle (void *dso_handle, const char *desc, const char *name)
{
	if (!dso_handle)
		return FALSE;

	char *symbol;
#if WINDOWS
	symbol = utils.monodroid_strdup_printf ("%s_%s", INITIALIZER_NAME, name);
#else
	symbol = utils.string_concat (INITIALIZER_NAME, "_", name);
#endif
	mono_bool result = load_profiler (dso_handle, desc, symbol);
#if WINDOWS
	free (symbol);
#else
	delete[] symbol;
#endif
	if (result)
		return TRUE;
	dlclose (dso_handle);
	return FALSE;
}

static void
monodroid_profiler_load (const char *libmono_path, const char *desc, const char *logfile)
{
	const char* col = strchr (desc, ':');
	char *mname;

	if (col != nullptr) {
		mname = new char [col - desc + 1];
		strncpy (mname, desc, col - desc);
		mname [col - desc] = 0;
	} else {
		mname = utils.strdup_new (desc);
	}

	int dlopen_flags = RTLD_LAZY;
	char *libname;
#if WINDOWS
	libname = utils.monodroid_strdup_printf ("libmono-profiler-%s.so", mname);
#else
	libname = utils.string_concat ("libmono-profiler-", mname, ".so");
#endif
	mono_bool found = 0;
	void *handle = androidSystem.load_dso_from_any_directories (libname, dlopen_flags);
	found = load_profiler_from_handle (handle, desc, mname);

	if (!found && libmono_path != nullptr) {
		char *full_path = utils.path_combine (libmono_path, libname);
		handle = androidSystem.load_dso (full_path, dlopen_flags, FALSE);
		delete[] full_path;
		found = load_profiler_from_handle (handle, desc, mname);
	}

	if (found && logfile != nullptr)
		utils.set_world_accessable (logfile);

	if (!found)
		log_warn (LOG_DEFAULT,
				"The '%s' profiler wasn't found in the main executable nor could it be loaded from '%s'.",
				mname,
				libname);

	delete[] mname;
	delete[] libname;
}

static void
set_profile_options (JNIEnv *env)
{
	char *value;
	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_PROFILE_PROPERTY, &value) == 0)
		return;

	char *output = nullptr;
	char **args = utils.monodroid_strsplit (value, ",", -1);
	for (char **ptr = args; ptr && *ptr; ptr++) {
		const char *arg = *ptr;
		if (!strncmp (arg, "output=", sizeof ("output=")-1)) {
			const char *p = arg + (sizeof ("output=")-1);
			if (strlen (p)) {
				output = utils.strdup_new (p);
				break;
			}
		}
	}
	utils.monodroid_strfreev (args);

	if (!output) {
		const char* col = strchr (value, ':');
		char *ovalue;
		char *extension;

		if ((col && !strncmp (value, "log:", 4)) || !strcmp (value, "log"))
			extension = utils.strdup_new ("mlpd");
		else {
			int len = col ? col - value - 1 : strlen (value);
			extension = new char [len + 1];
			strncpy (extension, value, len);
			extension [len] = '\0';
		}

		char *filename;
#if WINDOWS
		filename = utils.monodroid_strdup_printf ("profile.%s", extension);
#else
		filename = utils.string_concat ("profile.", extension);
#endif

		output = utils.path_combine (androidSystem.get_override_dir (0), filename);
#if WINDOWS
		ovalue = utils.monodroid_strdup_printf ("%s%soutput=%s",
				value,
				col == nullptr ? ":" : ",",
				output);
#else
		ovalue = utils.string_concat (value,
		                              col == nullptr ? ":" : ",",
		                              "output=",
		                              output);
#endif
		delete[] value;
		delete[] extension;
#if WINDOWS
		free (filename);
#else
		delete[] filename;
#endif
		value = ovalue;
	}

	/*
	 * libmono-profiler-log.so profiler won't overwrite existing files.
	 * Remove it For Great Justice^H^H^H to preserve my sanity!
	 */
	unlink (output);

	log_warn (LOG_DEFAULT, "Initializing profiler with options: %s", value);
	monodroid_profiler_load (runtime_libdir, value, output);

	free (value);
	free (output);
}

static FILE* counters;

/*
 * process_cmd:
 *
 *   Process a command received from XS through a socket connection.
 * This is called on a separate thread.
 * Return TRUE, if a new connection need to be opened.
 */
int
process_cmd (int fd, char *cmd)
{
	if (!strcmp (cmd, "connect output")) {
		dup2 (fd, 1);
		dup2 (fd, 2);
		return TRUE;
	} else if (!strcmp (cmd, "connect stdout")) {
		dup2 (fd, 1);
		return TRUE;
	} else if (!strcmp (cmd, "connect stderr")) {
		dup2 (fd, 2);
		return TRUE;
	} else if (!strcmp (cmd, "discard")) {
		return TRUE;
	} else if (!strcmp (cmd, "ping")) {
		if (!utils.send_uninterrupted (fd, const_cast<void*> (reinterpret_cast<const void*> ("pong")), 5))
			log_error (LOG_DEFAULT, "Got keepalive request from XS, but could not send response back (%s)\n", strerror (errno));
	} else if (!strcmp (cmd, "exit process")) {
		log_info (LOG_DEFAULT, "XS requested an exit, will exit immediately.\n");
		fflush (stdout);
		fflush (stderr);
		exit (0);
	} else if (!strncmp (cmd, "start debugger: ", 16)) {
		const char *debugger = cmd + 16;
		int use_fd = FALSE;
		if (!strcmp (debugger, "no")) {
			/* disabled */
		} else if (!strcmp (debugger, "sdb")) {
			sdb_fd = fd;
			use_fd = TRUE;
		}
		/* Notify the main thread (start_debugging ()) */
		debugging_configured = TRUE;
		pthread_mutex_lock (&process_cmd_mutex);
		pthread_cond_signal (&process_cmd_cond);
		pthread_mutex_unlock (&process_cmd_mutex);
		if (use_fd)
			return TRUE;
	} else if (!strncmp (cmd, "start profiler: ", 16)) {
		const char *prof = cmd + 16;
		int use_fd = FALSE;

		if (!strcmp (prof, "no")) {
			/* disabled */
		} else if (!strncmp (prof, "log:", 4)) {
			use_fd = TRUE;
			profiler_fd = fd;
			profiler_description = utils.monodroid_strdup_printf ("%s,output=#%i", prof, profiler_fd);
		} else {
			log_error (LOG_DEFAULT, "Unknown profiler: '%s'", prof);
		}
		/* Notify the main thread (start_profiling ()) */
		profiler_configured = TRUE;
		pthread_mutex_lock (&process_cmd_mutex);
		pthread_cond_signal (&process_cmd_cond);
		pthread_mutex_unlock (&process_cmd_mutex);
		if (use_fd)
			return TRUE;
	} else {
		log_error (LOG_DEFAULT, "Unsupported command: '%s'", cmd);
	}

	return FALSE;
}

#ifdef DEBUG

static void
start_debugging (void)
{
	char *debug_arg;
	char *debug_options [2];

	// wait for debugger configuration to finish
	pthread_mutex_lock (&process_cmd_mutex);
	while (!debugging_configured && !config_timedout) {
		if (pthread_cond_timedwait (&process_cmd_cond, &process_cmd_mutex, &wait_ts) == ETIMEDOUT)
			config_timedout = TRUE;
	}
	pthread_mutex_unlock (&process_cmd_mutex);

	if (!sdb_fd)
		return;

	embeddedAssemblies.set_register_debug_symbols (true);

	debug_arg = utils.monodroid_strdup_printf ("--debugger-agent=transport=socket-fd,address=%d,embedding=1", sdb_fd);
	debug_options[0] = debug_arg;

	log_warn (LOG_DEBUGGER, "Trying to initialize the debugger with options: %s", debug_arg);

	if (enable_soft_breakpoints ()) {
		constexpr char soft_breakpoints[] = "--soft-breakpoints";
		debug_options[1] = const_cast<char*> (soft_breakpoints);
		monoFunctions.jit_parse_options (2, debug_options);
	} else {
		monoFunctions.jit_parse_options (1, debug_options);
	}

	monoFunctions.debug_init (MONO_DEBUG_FORMAT_MONO);
}

static void
start_profiling (void)
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
	monodroid_profiler_load (runtime_libdir, profiler_description, nullptr);
}

#endif  // def DEBUG

/*
Disable LLVM signal handlers.

This happens when RenderScript needs to be compiled. See https://bugzilla.xamarin.com/show_bug.cgi?id=18016

This happens only on first run of the app. LLVM is used to compiled the RenderScript scripts. LLVM, been
a nice and smart library installs a ton of signal handlers and don't chain at all, completely breaking us.

This is a hack to set llvm::DisablePrettyStackTrace to true and avoid this source of signal handlers.

*/
static void
disable_external_signal_handlers (void)
{
	if (!androidSystem.is_mono_llvm_enabled ())
		return;

	void *llvm  = androidSystem.load_dso ("libLLVM.so", RTLD_LAZY, TRUE);
	if (llvm) {
		bool *disable_signals = reinterpret_cast<bool*> (dlsym (llvm, "_ZN4llvm23DisablePrettyStackTraceE"));
		if (disable_signals) {
			*disable_signals = true;
			log_info (LOG_DEFAULT, "Disabled LLVM signal trapping");
		}
		//MUST NOT dlclose to ensure we don't lose the hack
	}
}

MONO_API void
_monodroid_counters_dump (const char *format, ...)
{
	if (counters == nullptr)
		return;

	va_list args;
	fprintf (counters, "\n");

	va_start (args, format);
	vfprintf (counters, format, args);
	va_end (args);

	fprintf (counters, "\n");

	monoFunctions.counters_dump (XA_LOG_COUNTERS, counters);
}

static void
load_assembly (MonoDomain *domain, JNIEnv *env, jstring_wrapper &assembly)
{
	timing_period total_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING)))
		total_time.mark_start ();

	const char *assm_name = assembly.get_cstr ();
	MonoAssemblyName *aname;

	aname = monoFunctions.assembly_name_new (assm_name);

	if (domain != monoFunctions.domain_get ()) {
		MonoDomain *current = monoFunctions.domain_get ();
		monoFunctions.domain_set (domain, FALSE);
		monoFunctions.assembly_load_full (aname, NULL, NULL, 0);
		monoFunctions.domain_set (current, FALSE);
	} else {
		monoFunctions.assembly_load_full (aname, NULL, NULL, 0);
	}

	monoFunctions.assembly_name_free (aname);

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_end ();

		timing_diff diff (total_time);
		log_info (LOG_TIMING, "Assembly load: %s preloaded; elapsed: %lis:%lu::%lu", assm_name, diff.sec, diff.ms, diff.ns);
	}
}

static void
load_assemblies (MonoDomain *domain, JNIEnv *env, jstring_array_wrapper &assemblies)
{
	timing_period total_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING)))
		total_time.mark_start ();

	/* skip element 0, as that's loaded in create_domain() */
	for (size_t i = 1; i < assemblies.get_length (); ++i) {
		jstring_wrapper &assembly = assemblies [i];
		load_assembly (domain, env, assembly);
	}

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_end ();

		timing_diff diff (total_time);
		log_info (LOG_TIMING, "Finished loading assemblies: preloaded %u assemblies; wasted time: %lis:%lu::%lu", assemblies.get_length (), diff.sec, diff.ms, diff.ns);
	}
}

static void
monodroid_Mono_UnhandledException_internal (MonoException *ex)
{
	// Do nothing with it here, we let the exception naturally propagate on the managed side
}

static MonoDomain*
create_and_initialize_domain (JNIEnv* env, jclass runtimeClass, jstring_array_wrapper &runtimeApks, jstring_array_wrapper &assemblies, jobject loader, bool is_root_domain)
{
	MonoDomain* domain = create_domain (env, runtimeClass, runtimeApks, loader, is_root_domain);

	// When running on desktop, the root domain is only a dummy so don't initialize it
	if (is_running_on_desktop && is_root_domain)
		return domain;

	if (androidSystem.is_assembly_preload_enabled ())
		load_assemblies (domain, env, assemblies);
	init_android_runtime (domain, env, runtimeClass, loader);

	osBridge.add_monodroid_domain (domain);

	return domain;
}

JNIEXPORT void JNICALL
Java_mono_android_Runtime_init (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
                                jstring runtimeNativeLibDir, jobjectArray appDirs, jobject loader,
                                jobjectArray externalStorageDirs, jobjectArray assembliesJava, jstring packageName,
                                jint apiLevel, jobjectArray environmentVariables)
{
	init_logging_categories ();

	timing_period total_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_start ();
	}

	android_api_level = apiLevel;

	TimeZone_class = utils.get_class_from_runtime_field (env, klass, "java_util_TimeZone", true);

	jstring_wrapper jstr (env, packageName);
	utils.monodroid_store_package_name (jstr.get_cstr ());

	jstr = lang;
	set_environment_variable (env, "LANG", jstr);

	androidSystem.setup_environment (env, environmentVariables);

	jstr = reinterpret_cast <jstring> (env->GetObjectArrayElement (appDirs, 1));
	set_environment_variable_for_directory (env, "TMPDIR", jstr);

	jstr = reinterpret_cast<jstring> (env->GetObjectArrayElement (appDirs, 0));
	set_environment_variable_for_directory (env, "HOME", jstr);
	create_xdg_directories_and_environment (env, jstr);
	primary_override_dir = get_primary_override_dir (env, jstr);

	disable_external_signal_handlers ();

	jstring_array_wrapper runtimeApks (env, runtimeApksJava);
	if (android_api_level < 23 || !androidSystem.is_embedded_dso_mode_enabled ()) {
		log_info (LOG_DEFAULT, "Setting up for DSO lookup in app data directories");
		jstr = env->GetObjectArrayElement (appDirs, 2);
		AndroidSystem::app_lib_directories_size = 1;
		AndroidSystem::app_lib_directories = (const char**) xcalloc (AndroidSystem::app_lib_directories_size, sizeof(char*));
		AndroidSystem::app_lib_directories [0] = strdup (jstr.get_cstr ());
	} else {
		log_info (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK");
		AndroidSystem::app_lib_directories_size = runtimeApks.get_length ();
		AndroidSystem::app_lib_directories = (const char**) xcalloc (AndroidSystem::app_lib_directories_size, sizeof(char*));

		unsigned short built_for_cpu = 0, running_on_cpu = 0;
		unsigned char is64bit = 0;
		_monodroid_detect_cpu_and_architecture (&built_for_cpu, &running_on_cpu, &is64bit);
		androidSystem.setup_apk_directories (env, running_on_cpu, runtimeApks);
	}

	jstr = env->GetObjectArrayElement (externalStorageDirs, 0);
	external_override_dir = strdup (jstr.get_cstr ());

	jstr = env->GetObjectArrayElement (externalStorageDirs, 1);
	external_legacy_override_dir = strdup (jstr.get_cstr ());

	init_reference_logging (primary_override_dir);
	androidSystem.create_update_dir (primary_override_dir);

#if DEBUG
	setup_gc_logging ();
	set_debug_env_vars ();
#endif

#ifndef RELEASE
	androidSystem.set_override_dir (1, external_override_dir);
	androidSystem.set_override_dir (2, external_legacy_override_dir);
	for (uint32_t i = 0; i < AndroidSystem::MAX_OVERRIDES; ++i) {
		const char *p = androidSystem.get_override_dir (i);
		if (!utils.directory_exists (p))
			continue;
		log_warn (LOG_DEFAULT, "Using override path: %s", p);
	}
#endif
	setup_bundled_app ("libmonodroid_bundle_app.so");

	if (runtimeNativeLibDir != nullptr) {
		jstr = runtimeNativeLibDir;
		runtime_libdir = strdup (jstr.get_cstr ());
		log_warn (LOG_DEFAULT, "Using runtime path: %s", runtime_libdir);
	}

	void *libmonosgen_handle = nullptr;

	/*
	 * We need to use RTLD_GLOBAL so that libmono-profiler-log.so can resolve
	 * symbols against the Mono library we're loading.
	 */
	int sgen_dlopen_flags = RTLD_LAZY | RTLD_GLOBAL;
	if (androidSystem.is_embedded_dso_mode_enabled ()) {
		libmonosgen_handle = androidSystem.load_dso_from_any_directories (AndroidSystem::MONO_SGEN_SO, sgen_dlopen_flags);
	}

	if (libmonosgen_handle == nullptr)
		libmonosgen_handle = androidSystem.load_dso (androidSystem.get_libmonosgen_path (), sgen_dlopen_flags, FALSE);

	if (!monoFunctions.init (libmonosgen_handle)) {
		log_fatal (LOG_DEFAULT, "shared runtime initialization error: %s", dlerror ());
		exit (FATAL_EXIT_CANNOT_FIND_MONO);
	}
	androidSystem.setup_process_args (env, runtimeApks);

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING)) && !(log_timing_categories & LOG_TIMING_BARE)) {
		monoFunctions.counters_enable (XA_LOG_COUNTERS);
		char *counters_path = utils.path_combine (androidSystem.get_override_dir (0), "counters.txt");
		log_info_nocheck (LOG_TIMING, "counters path: %s", counters_path);
		counters = utils.monodroid_fopen (counters_path, "a");
		utils.set_world_accessable (counters_path);
		delete[] counters_path;
	}

	monoFunctions.dl_fallback_register (monodroid_dlopen, monodroid_dlsym, nullptr, nullptr);

	set_profile_options (env);

	set_trace_options ();

#if defined (DEBUG) && !defined (WINDOWS)
	char *connect_args;
	utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_CONNECT_PROPERTY, &connect_args);

	if (connect_args != nullptr) {
		int res = debug.start_connection (connect_args);
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
#endif

	monoFunctions.config_parse_memory (reinterpret_cast<const char*> (monodroid_config));
	monoFunctions.register_machine_config (reinterpret_cast<const char*> (monodroid_machine_config));

	log_info (LOG_DEFAULT, "Probing for Mono AOT mode\n");

	MonoAotMode mode = androidSystem.get_mono_aot_mode ();
	if (mode == MonoAotMode::MONO_AOT_MODE_UNKNOWN)
		mode = MonoAotMode::MONO_AOT_MODE_NONE;

	if (mode != MonoAotMode::MONO_AOT_MODE_NORMAL && mode != MonoAotMode::MONO_AOT_MODE_NONE) {
		log_info (LOG_DEFAULT, "Enabling AOT mode in Mono");
		monoFunctions.jit_set_aot_mode (mode);
	}

	log_info (LOG_DEFAULT, "Probing if we should use LLVM\n");

	if (androidSystem.is_mono_llvm_enabled ()) {
		char *args [1];
		args[0] = const_cast<char*> ("--llvm");
		log_info (LOG_DEFAULT, "Enabling LLVM mode in Mono\n");
		monoFunctions.jit_parse_options (1,  args);
		monoFunctions.set_use_llvm (true);
	}

	char *runtime_args = nullptr;
	utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_EXTRA_PROPERTY, &runtime_args);
#if TRACE
	__android_log_print (ANDROID_LOG_INFO, "*jonp*", "debug.mono.extra=%s", runtime_args);
#endif

	timing_period partial_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING)))
		partial_time.mark_start ();

	mono_runtime_init (runtime_args);

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		partial_time.mark_end ();

		timing_diff diff (partial_time);
		log_info_nocheck (LOG_TIMING, "Runtime.init: Mono runtime init; elapsed: %lis:%lu::%lu", diff.sec, diff.ms, diff.ns);
	}

	jstring_array_wrapper assemblies (env, assembliesJava);
	/* the first assembly is used to initialize the AppDomain name */
	create_and_initialize_domain (env, klass, runtimeApks, assemblies, loader, /*is_root_domain:*/ true);

	delete[] runtime_args;

	// Install our dummy exception handler on Desktop
	if (is_running_on_desktop) {
		monoFunctions.add_internal_call ("System.Diagnostics.Debugger::Mono_UnhandledException_internal(System.Exception)",
		                                 reinterpret_cast<const void*> (monodroid_Mono_UnhandledException_internal));
	}

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_end ();

		timing_diff diff (total_time);
		log_info_nocheck (LOG_TIMING, "Runtime.init: end, total time; elapsed: %lis:%lu::%lu", diff.sec, diff.ms, diff.ns);
		_monodroid_counters_dump ("## Runtime.init: end");
	}
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_register (JNIEnv *env, jclass klass, jstring managedType, jclass nativeClass, jstring methods)
{
	int managedType_len, methods_len;
	const jchar *managedType_ptr, *methods_ptr;
	void *args [5];
	const char *mt_ptr;
	MonoDomain *domain = monoFunctions.domain_get ();
	timing_period total_time;

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING)))
		total_time.mark_start ();

	managedType_len = env->GetStringLength (managedType);
	managedType_ptr = env->GetStringChars (managedType, nullptr);

	methods_len = env->GetStringLength (methods);
	methods_ptr = env->GetStringChars (methods, nullptr);

	args [0] = &managedType_ptr,
	args [1] = &managedType_len;
	args [2] = &nativeClass;
	args [3] = &methods_ptr;
	args [4] = &methods_len;

	monoFunctions.jit_thread_attach (domain);
	// Refresh current domain as it might have been modified by the above call
	domain = monoFunctions.domain_get ();
	utils.monodroid_runtime_invoke (domain, registerType, nullptr, args, nullptr);

	env->ReleaseStringChars (methods, methods_ptr);
	env->ReleaseStringChars (managedType, managedType_ptr);

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_end ();

		timing_diff diff (total_time);

		mt_ptr = env->GetStringUTFChars (managedType, nullptr);
		char *type = utils.strdup_new (mt_ptr);
		env->ReleaseStringUTFChars (managedType, mt_ptr);

		log_info_nocheck (LOG_TIMING, "Runtime.register: end time; elapsed: %lis:%lu::%lu", diff.sec, diff.ms, diff.ns);
		_monodroid_counters_dump ("## Runtime.register: type=%s\n", type);
		delete[] type;
	}
}

// DO NOT USE ON NORMAL X.A
// This function only works with the custom TypeManager embedded with the designer process.
static void
reinitialize_android_runtime_type_manager (JNIEnv *env)
{
	jclass typeManager = env->FindClass ("mono/android/TypeManager");
	env->UnregisterNatives (typeManager);

	jmethodID resetRegistration = env->GetStaticMethodID (typeManager, "resetRegistration", "()V");
	env->CallStaticVoidMethod (typeManager, resetRegistration);

	env->DeleteLocalRef (typeManager);
}

JNIEXPORT jint
JNICALL Java_mono_android_Runtime_createNewContext (JNIEnv *env, jclass klass, jobjectArray runtimeApksJava, jobjectArray assembliesJava, jobject loader)
{
	log_info (LOG_DEFAULT, "CREATING NEW CONTEXT");
	reinitialize_android_runtime_type_manager (env);
	MonoDomain *root_domain = monoFunctions.get_root_domain ();
	monoFunctions.jit_thread_attach (root_domain);

	jstring_array_wrapper runtimeApks (env, runtimeApksJava);
	jstring_array_wrapper assemblies (env, assembliesJava);
	MonoDomain *domain = create_and_initialize_domain (env, klass, runtimeApks, assemblies, loader, /*is_root_domain:*/ false);
	monoFunctions.domain_set (domain, FALSE);
	int domain_id = monoFunctions.domain_get_id (domain);
	current_context_id = domain_id;
	log_info (LOG_DEFAULT, "Created new context with id %d\n", domain_id);
	return domain_id;
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_switchToContext (JNIEnv *env, jclass klass, jint contextID)
{
	log_info (LOG_DEFAULT, "SWITCHING CONTEXT");
	MonoDomain *domain = monoFunctions.domain_get_by_id ((int)contextID);
	if (current_context_id != (int)contextID) {
		monoFunctions.domain_set (domain, TRUE);
		// Reinitialize TypeManager so that its JNI handle goes into the right domain
		reinitialize_android_runtime_type_manager (env);
	}
	current_context_id = (int)contextID;
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_destroyContexts (JNIEnv *env, jclass klass, jintArray array)
{
	MonoDomain *root_domain = monoFunctions.get_root_domain ();
	monoFunctions.jit_thread_attach (root_domain);
	current_context_id = -1;

	jint *contextIDs = env->GetIntArrayElements (array, nullptr);
	jsize count = env->GetArrayLength (array);

	log_info (LOG_DEFAULT, "Cleaning %d domains", count);

	int i;
	for (i = 0; i < count; i++) {
		int domain_id = contextIDs[i];
		MonoDomain *domain = monoFunctions.domain_get_by_id (domain_id);

		if (domain == nullptr)
			continue;
		log_info (LOG_DEFAULT, "Shutting down domain `%d'", contextIDs[i]);
		shutdown_android_runtime (domain);
		osBridge.remove_monodroid_domain (domain);
	}
	osBridge.on_destroy_contexts ();

	for (i = 0; i < count; i++) {
		int domain_id = contextIDs[i];
		MonoDomain *domain = monoFunctions.domain_get_by_id (domain_id);

		if (domain == nullptr)
			continue;
		log_info (LOG_DEFAULT, "Unloading domain `%d'", contextIDs[i]);
		monoFunctions.domain_unload (domain);
	}

	env->ReleaseIntArrayElements (array, contextIDs, JNI_ABORT);

	reinitialize_android_runtime_type_manager (env);

	log_info (LOG_DEFAULT, "All domain cleaned up");
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_propagateUncaughtException (JNIEnv *env, jclass klass, jobject javaThread, jthrowable javaException)
{
	MonoDomain *domain = monoFunctions.domain_get ();
	propagate_uncaught_exception (domain, env, javaThread, javaException);
}

extern "C" DylibMono* monodroid_dylib_mono_new (const char *libmono_path)
{
	DylibMono *imports = new DylibMono ();
	if (!imports)
		return nullptr;

	void *libmono_handle = androidSystem.load_dso_from_any_directories(libmono_path, RTLD_LAZY | RTLD_GLOBAL);
	if (!imports->init (libmono_handle)) {
		delete imports;
		return nullptr;
	}

	return imports;
}

extern "C" void monodroid_dylib_mono_free (DylibMono *mono_imports)
{
	if (!mono_imports)
		return;

	mono_imports->close ();
	delete mono_imports;
}

/*
  this function is used from JavaInterop and should be treated as public API
  https://github.com/xamarin/java.interop/blob/master/src/java-interop/java-interop-gc-bridge-mono.c#L266

  it should also accept libmono_path = nullptr parameter
*/
extern "C" int monodroid_dylib_mono_init (DylibMono *mono_imports, const char *libmono_path)
{
	if (mono_imports == nullptr)
		return FALSE;

	void *libmono_handle = libmono_path ? androidSystem.load_dso_from_any_directories(libmono_path, RTLD_LAZY | RTLD_GLOBAL) : dlopen (libmono_path, RTLD_LAZY | RTLD_GLOBAL);;
	return mono_imports->init (libmono_handle) ? TRUE : FALSE;
}

extern "C" DylibMono*  monodroid_get_dylib (void)
{
	return &monoFunctions;
}

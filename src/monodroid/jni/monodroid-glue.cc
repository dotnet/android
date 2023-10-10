#include <array>
#include <cstdlib>
#include <cstdarg>

#include <jni.h>
#include <ctime>
#include <cstdio>
#include <cstring>
#include <strings.h>
#include <cctype>
#include <cerrno>
#if !defined (__MINGW32__) || (defined (__MINGW32__) && __GNUC__ >= 10)
#include <compare>
#endif // ndef MINGW32 || def MINGW32 && GNUC >= 10
#if defined (APPLE_OS_X)
#include <dlfcn.h>
#endif  // def APPLE_OX_X

#include <fcntl.h>
#include <unistd.h>
#include <cstdint>

#include <sys/time.h>
#include <sys/types.h>

#include <mono/jit/jit.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/class.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/mono-config.h>
#include <mono/metadata/mono-debug.h>
#include <mono/metadata/object.h>
#include <mono/utils/mono-dl-fallback.h>
#include <mono/utils/mono-logger.h>

#if defined (NET)
#include <mono/metadata/mono-private-unstable.h>
#endif

#include "mono_android_Runtime.h"

#if defined (DEBUG) && !defined (WINDOWS)
#include <fcntl.h>
#include <arpa/inet.h>
#include <sys/socket.h>
#include <netinet/in.h>
#endif

#if defined (__linux__) || defined (__linux)
#include <sys/syscall.h>
#endif

#if defined (APPLE_OS_X)
#include <libgen.h>
#endif  // defined(APPLE_OS_X)

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

#include "java-interop-util.h"
#include "logger.hh"

#include "monodroid.h"
#include "util.hh"
#include "debug.hh"
#include "embedded-assemblies.hh"
#include "monodroid-glue.hh"
#include "monodroid-glue-internal.hh"
#include "globals.hh"
#include "xamarin-app.hh"
#include "timing.hh"
#include "xa-internal-api-impl.hh"
#include "build-info.hh"
#include "monovm-properties.hh"
#include "startup-aware-lock.hh"
#include "timing-internal.hh"

#ifndef WINDOWS
#include "xamarin_getifaddrs.h"
#endif

#include "cpp-util.hh"
#include "strings.hh"

#include "java-interop-dlfcn.h"

using namespace microsoft::java_interop;
using namespace xamarin::android;
using namespace xamarin::android::internal;

#if !defined (NET)
#include "config.include"
#include "machine.config.include"

std::mutex MonodroidRuntime::api_init_lock;
void *MonodroidRuntime::api_dso_handle = nullptr;
#else // ndef NET
std::mutex MonodroidRuntime::pinvoke_map_write_lock;

MonoCoreRuntimeProperties MonodroidRuntime::monovm_core_properties = {
	.trusted_platform_assemblies = nullptr,
	.app_paths = nullptr,
	.native_dll_search_directories = nullptr,
	.pinvoke_override = &MonodroidRuntime::monodroid_pinvoke_override
};

#endif // def NET

std::mutex MonodroidRuntime::dso_handle_write_lock;
bool MonodroidRuntime::startup_in_progress = true;

#ifdef WINDOWS
static const char* get_xamarin_android_msbuild_path (void);
const char *BasicAndroidSystem::SYSTEM_LIB_PATH = get_xamarin_android_msbuild_path();

/* Set of Windows-specific utility/reimplementation of Unix functions */

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
	if (base_path == nullptr) {
		log_fatal (LOG_DEFAULT, "Failed to convert UTF-16 to UTF-8 in %s", __PRETTY_FUNCTION__);
		Helpers::abort_application ();
	}
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

void
MonodroidRuntime::thread_start ([[maybe_unused]] MonoProfiler *prof, [[maybe_unused]] uintptr_t tid)
{
	JNIEnv* env;
	int r;
#ifdef PLATFORM_ANDROID
	r = osBridge.get_jvm ()->AttachCurrentThread (&env, nullptr);
#else   // ndef PLATFORM_ANDROID
	r = osBridge.get_jvm ()->AttachCurrentThread (reinterpret_cast<void**>(&env), nullptr);
#endif  // ndef PLATFORM_ANDROID
	if (r != JNI_OK) {
#if DEBUG
		log_fatal (LOG_DEFAULT, "ERROR: Unable to attach current thread to the Java VM!");
		Helpers::abort_application ();
#endif
	}
}

void
MonodroidRuntime::thread_end ([[maybe_unused]] MonoProfiler *prof, [[maybe_unused]] uintptr_t tid)
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

inline void
MonodroidRuntime::log_jit_event (MonoMethod *method, const char *event_name)
{
	jit_time.mark_end ();

	if (jit_log == nullptr)
		return;

	char* name = mono_method_full_name (method, 1);

	timing_diff diff (jit_time);
	fprintf (jit_log, "JIT method %6s: %s elapsed: %lis:%u::%u\n", event_name, name, static_cast<long>(diff.sec), diff.ms, diff.ns);

	free (name);
}

void
MonodroidRuntime::jit_begin ([[maybe_unused]] MonoProfiler *prof, MonoMethod *method)
{
	monodroidRuntime.log_jit_event (method, "begin");
}

void
MonodroidRuntime::jit_failed ([[maybe_unused]] MonoProfiler *prof, MonoMethod *method)
{
	monodroidRuntime.log_jit_event (method, "failed");
}

void
MonodroidRuntime::jit_done ([[maybe_unused]] MonoProfiler *prof, MonoMethod *method, [[maybe_unused]] MonoJitInfo* jinfo)
{
	monodroidRuntime.log_jit_event (method, "done");
}

#ifndef RELEASE
MonoAssembly*
MonodroidRuntime::open_from_update_dir (MonoAssemblyName *aname, [[maybe_unused]] char **assemblies_path, [[maybe_unused]] void *user_data)
{
	MonoAssembly *result = nullptr;

#ifndef ANDROID
	// First check if there are any in-memory assemblies
	if (designerAssemblies.has_assemblies ()) {
		MonoDomain *domain = mono_domain_get ();
		result = designerAssemblies.try_load_assembly (domain, aname);
		if (result != nullptr) {
			log_debug (LOG_ASSEMBLY, "Loaded assembly %s from memory in domain %d", mono_assembly_name_get_name (aname), mono_domain_get_id (domain));
			return result;
		}
		log_debug (LOG_ASSEMBLY, "No in-memory data found for assembly %s", mono_assembly_name_get_name (aname));
	} else {
		log_debug (LOG_ASSEMBLY, "No in-memory assemblies detected", mono_assembly_name_get_name (aname));
	}
#endif
	const char *override_dir;
	bool found = false;

	for (uint32_t oi = 0; oi < AndroidSystem::MAX_OVERRIDES; ++oi) {
		override_dir = androidSystem.get_override_dir (oi);
		if (override_dir != nullptr && utils.directory_exists (override_dir)) {
			found = true;
			break;
		}
	}
	if (!found)
		return nullptr;

	const char *culture = reinterpret_cast<const char*> (mono_assembly_name_get_culture (aname));
	const char *name    = reinterpret_cast<const char*> (mono_assembly_name_get_name (aname));
	size_t culture_len;

	if (culture != nullptr)
		culture_len = strlen (culture);
	else
		culture_len = 0;
	size_t name_len = strlen (name);

	static_local_string<SENSIBLE_PATH_MAX> pname (name_len + culture_len);
	if (culture_len > 0) {
		pname.append (culture, culture_len);
		pname.append ("/");
	}
	pname.append (name, name_len);

	constexpr size_t dll_extension_len = sizeof(SharedConstants::DLL_EXTENSION) - 1;

	bool is_dll = utils.ends_with (name, SharedConstants::DLL_EXTENSION);
	size_t file_name_len = pname.length () + 1;
	if (!is_dll)
		file_name_len += dll_extension_len;

	for (uint32_t oi = 0; oi < AndroidSystem::MAX_OVERRIDES; ++oi) {
		override_dir = androidSystem.get_override_dir (oi);
		if (override_dir == nullptr || !utils.directory_exists (override_dir))
			continue;

		size_t override_dir_len = strlen (override_dir);
		static_local_string<SENSIBLE_PATH_MAX> fullpath (override_dir_len + file_name_len);
		utils.path_combine (fullpath, override_dir, override_dir_len, pname.get (), pname.length ());
		if (!is_dll) {
			fullpath.append (SharedConstants::DLL_EXTENSION, dll_extension_len);
		}

		log_info (LOG_ASSEMBLY, "open_from_update_dir: trying to open assembly: %s\n", fullpath.get ());
		if (utils.file_exists (fullpath.get ()))
			result = mono_assembly_open_full (fullpath.get (), nullptr, 0);
		if (result != nullptr) {
			// TODO: register .mdb, .pdb file
			break;
		}
	}

	if (result && utils.should_log (LOG_ASSEMBLY)) {
		log_info_nocheck (LOG_ASSEMBLY, "open_from_update_dir: loaded assembly: %p\n", result);
	}
	return result;
}
#endif

bool
MonodroidRuntime::should_register_file ([[maybe_unused]] const char *filename)
{
#ifndef RELEASE
	if (filename == nullptr) {
		return true;
	}

	size_t filename_len = strlen (filename) + 1; // includes space for path separator
	for (size_t i = 0; i < AndroidSystem::MAX_OVERRIDES; ++i) {
		const char *odir = androidSystem.get_override_dir (i);
		if (odir == nullptr)
			continue;

		size_t odir_len = strlen (odir);
		static_local_string<SENSIBLE_PATH_MAX> p (odir_len + filename_len);
		utils.path_combine (p, odir, odir_len, filename, filename_len);
		bool  exists  = utils.file_exists (p.get ());

		if (exists) {
			log_info (LOG_ASSEMBLY, "should not register '%s' as it exists in the override directory '%s'", filename, odir);
			return !exists;
		}
	}
#endif
	return true;
}

inline void
MonodroidRuntime::gather_bundled_assemblies (jstring_array_wrapper &runtimeApks, size_t *out_user_assemblies_count, bool have_split_apks)
{
#if defined(DEBUG) || !defined (ANDROID)
	if (application_config.instant_run_enabled) {
		for (size_t i = 0; i < AndroidSystem::MAX_OVERRIDES; ++i) {
			const char *p = androidSystem.get_override_dir (i);
			if (p == nullptr || !utils.directory_exists (p))
				continue;
			log_info (LOG_ASSEMBLY, "Loading TypeMaps from %s", p);
			embeddedAssemblies.try_load_typemaps_from_directory (p);
		}
	}
#endif

	int64_t apk_count = static_cast<int64_t>(runtimeApks.get_length ());
	size_t prev_num_assemblies = 0;
	bool got_split_config_abi_apk = false;
	bool got_base_apk = false;

	for (int64_t i = 0; i < apk_count; i++) {
		jstring_wrapper &apk_file = runtimeApks [static_cast<size_t>(i)];

		if (have_split_apks) {
			bool scan_apk = false;

			if (!got_split_config_abi_apk && utils.ends_with (apk_file.get_cstr (), SharedConstants::split_config_abi_apk_name)) {
				got_split_config_abi_apk = scan_apk = true;
			} else if (!got_base_apk && utils.ends_with (apk_file.get_cstr (), base_apk_name)) {
				got_base_apk = scan_apk = true;
			}

			if (!scan_apk) {
				continue;
			}
		}

		size_t cur_num_assemblies  = embeddedAssemblies.register_from<should_register_file> (apk_file.get_cstr ());

		*out_user_assemblies_count += (cur_num_assemblies - prev_num_assemblies);
		prev_num_assemblies = cur_num_assemblies;

		if (!embeddedAssemblies.keep_scanning ()) {
			break;
		}
	}
}

#if defined (DEBUG) && !defined (WINDOWS)
int
MonodroidRuntime::monodroid_debug_connect (int sock, struct sockaddr_in addr)
{
	long flags = fcntl (sock, F_GETFL, nullptr);
	flags |= O_NONBLOCK;
	fcntl (sock, F_SETFL, flags);

	int res = connect (sock, (struct sockaddr *) &addr, sizeof (addr));

	if (res < 0) {
		if (errno == EINPROGRESS) {
			while (true) {
				timeval tv;

				tv.tv_sec = 2;
				tv.tv_usec = 0;

				fd_set fds;
				FD_ZERO (&fds);
				FD_SET (sock, &fds);

				res = select (sock + 1, 0, &fds, 0, &tv);

				if (res <= 0 && errno != EINTR) return -5;

				socklen_t len = sizeof (int);
				int val = 0;
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
MonodroidRuntime::monodroid_debug_accept (int sock, struct sockaddr_in addr)
{
	ssize_t res = bind (sock, (struct sockaddr *) &addr, sizeof (addr));
	if (res < 0)
		return -1;

	res = listen (sock, 1);
	if (res < 0)
		return -2;

	int accepted = accept (sock, nullptr, nullptr);
	if (accepted < 0)
		return -3;

	constexpr const char handshake_msg [] = "MonoDroid-Handshake\n";
	constexpr size_t handshake_length = sizeof (handshake_msg) - 1;

	do {
		res = send (accepted, handshake_msg, handshake_length, 0);
	} while (res == -1 && errno == EINTR);
	if (res < 0)
		return -4;

	return accepted;
}
#endif

inline jint
MonodroidRuntime::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved)
{
	JNIEnv *env;

	androidSystem.init_max_gref_count ();

	vm->GetEnv ((void**)&env, JNI_VERSION_1_6);
	osBridge.initialize_on_onload (vm, env);

	return JNI_VERSION_1_6;
}

void
MonodroidRuntime::parse_gdb_options ()
{
	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> val;

	if (!(androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_GDB_PROPERTY, val) > 0))
		return;

	constexpr char wait_param[] = "wait:";
	constexpr size_t wait_param_length = sizeof(wait_param) - 1;

	if (val.starts_with (wait_param)) {
		/*
		 * The form of the property should be: 'wait:<timestamp>', where <timestamp> should be
		 * the output of date +%s in the android shell.
		 * If this property is set, wait for a native debugger to attach by spinning in a loop.
		 * The debugger can break the wait by setting 'monodroid_gdb_wait' to 0.
		 * If the current time is later than <timestamp> + 10s, the property is ignored.
		 */
		bool do_wait = true;

		long long v = atoll (val.get () + wait_param_length);
		if (v > 100000) {
			time_t secs = time (nullptr);

			if (v + 10 < secs) {
				log_warn (LOG_DEFAULT, "Found stale %s property with value '%s', not waiting.", Debug::DEBUG_MONO_GDB_PROPERTY, val.get ());
				do_wait = false;
			}
		}

		wait_for_gdb = do_wait;
	}
}

#if defined (DEBUG) && !defined (WINDOWS)
bool
MonodroidRuntime::parse_runtime_args (dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> &runtime_args, RuntimeOptions *options)
{
	if (runtime_args.length () == 0) {
		log_warn (LOG_DEFAULT, "runtime args empty");
		return true;
	}

	constexpr char   ARG_DEBUG[]                   = "debug";
	constexpr size_t ARG_DEBUG_LENGTH              = sizeof(ARG_DEBUG) - 1;
	constexpr char   ARG_TIMEOUT[]                 = "timeout=";
	constexpr size_t ARG_TIMEOUT_LENGTH            = sizeof(ARG_TIMEOUT) - 1;
	constexpr char   ARG_SERVER[]                  = "server=";
	constexpr size_t ARG_SERVER_LENGTH             = sizeof(ARG_SERVER) - 1;
	constexpr char   ARG_LOGLEVEL[]                = "loglevel=";
	constexpr size_t ARG_LOGLEVEL_LENGTH           = sizeof(ARG_LOGLEVEL) - 1;

	bool ret = true;
	string_segment token;
	while (runtime_args.next_token (',', token)) {
		if (token.starts_with (ARG_DEBUG, ARG_DEBUG_LENGTH)) {
			char *host = nullptr;
			int sdb_port = 1000, out_port = -1;

			options->debug = true;

			if (token.has_at ('=', ARG_DEBUG_LENGTH)) {
				constexpr size_t arg_name_length = ARG_DEBUG_LENGTH + 1; // Includes the '='

				static_local_string<SMALL_STRING_PARSE_BUFFER_LEN> hostport (token.length () - arg_name_length);
				hostport.assign (token.start () + arg_name_length, token.length () - arg_name_length);

				string_segment address;
				size_t field = 0;
				while (field < 3 && hostport.next_token (':', address)) {
					switch (field) {
						case 0: // host
							if (address.empty ()) {
								log_error (LOG_DEFAULT, "Invalid --debug argument for the host field (empty string)");
							} else {
								host = utils.strdup_new (address.start (), address.length ());
							}
							break;

						case 1: // sdb_port
							if (!address.to_integer (sdb_port)) {
								log_error (LOG_DEFAULT, "Invalid --debug argument for the sdb_port field");
							}
							break;

						case 2: // out_port
							if (!address.to_integer (out_port)) {
								log_error (LOG_DEFAULT, "Invalid --debug argument for the sdb_port field");
							}
							break;
					}
					field++;
				}
			} else if (!token.has_at ('\0', ARG_DEBUG_LENGTH)) {
				log_error (LOG_DEFAULT, "Invalid --debug argument.");
				ret = false;
				continue;
			}

			if (sdb_port < 0 || sdb_port > std::numeric_limits<unsigned short>::max ()) {
				log_error (LOG_DEFAULT, "Invalid SDB port value %d", sdb_port);
				ret = false;
				continue;
			}

			if (out_port > std::numeric_limits<unsigned short>::max ()) {
				log_error (LOG_DEFAULT, "Invalid output port value %d", out_port);
				ret = false;
				continue;
			}

			if (host == nullptr)
				host = utils.strdup_new ("10.0.2.2");

			options->host = host;
			options->sdb_port = static_cast<uint16_t>(sdb_port);
			options->out_port = out_port == -1 ? 0 : static_cast<uint16_t>(out_port);
		} else if (token.starts_with (ARG_TIMEOUT, ARG_TIMEOUT_LENGTH)) {
			if (!token.to_integer (options->timeout_time, ARG_TIMEOUT_LENGTH)) {
				log_error (LOG_DEFAULT, "Invalid --timeout argument.");
				ret = false;
			}
		} else if (token.starts_with (ARG_SERVER, ARG_SERVER_LENGTH)) {
			options->server = token.has_at ('y', ARG_SERVER_LENGTH) || token.has_at ('Y', ARG_SERVER_LENGTH);
		} else if (token.starts_with (ARG_LOGLEVEL, ARG_LOGLEVEL_LENGTH)) {
			if (!token.to_integer (options->loglevel, ARG_LOGLEVEL_LENGTH)) {
				log_error (LOG_DEFAULT, "Invalid --loglevel argument.");
				ret = false;
			}
		} else {
			static_local_string<SMALL_STRING_PARSE_BUFFER_LEN> arg (token);
			log_error (LOG_DEFAULT, "Unknown runtime argument: '%s'", arg.get ());
			ret = false;
		}
	}

	return ret;
}
#endif  // def DEBUG && !WINDOWS

inline void
MonodroidRuntime::set_debug_options (void)
{
	if (androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_DEBUG_PROPERTY, nullptr) == 0)
		return;

	embeddedAssemblies.set_register_debug_symbols (true);
	mono_debug_init (MONO_DEBUG_FORMAT_MONO);
}

void
MonodroidRuntime::mono_runtime_init ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN>& runtime_args)
{
#if defined (DEBUG) && !defined (WINDOWS)
	RuntimeOptions options{};
	int64_t cur_time;

	cur_time = time (nullptr);

	if (!parse_runtime_args (runtime_args, &options)) {
		log_error (LOG_DEFAULT, "Failed to parse runtime args: '%s'", runtime_args.get ());
	} else if (options.debug && cur_time > options.timeout_time) {
		log_warn (LOG_DEBUGGER, "Not starting the debugger as the timeout value has been reached; current-time: %lli  timeout: %lli", cur_time, options.timeout_time);
	} else if (options.debug && cur_time <= options.timeout_time) {
		embeddedAssemblies.set_register_debug_symbols (true);

		int loglevel;
		if (debug.have_debugger_log_level ())
			loglevel = debug.get_debugger_log_level ();
		else
			loglevel = options.loglevel;

		char *debug_arg = utils.monodroid_strdup_printf (
			"--debugger-agent=transport=dt_socket,loglevel=%d,address=%s:%d,%sembedding=1,timeout=%d",
			loglevel,
			options.host,
			options.sdb_port,
			options.server ? "server=y," : "",
			options.timeout_time
		);

		char *debug_options [2] = {
			debug_arg,
			nullptr
		};

		// this text is used in unit tests to check the debugger started
		// do not change it without updating the test.
		log_warn (LOG_DEBUGGER, "Trying to initialize the debugger with options: %s", debug_arg);

		if (options.out_port > 0) {
			int sock = socket (PF_INET, SOCK_STREAM, IPPROTO_TCP);
			if (sock < 0) {
				log_fatal (LOG_DEBUGGER, "Could not construct a socket for stdout and stderr; does your app have the android.permission.INTERNET permission? %s", strerror (errno));
				Helpers::abort_application ();
			}

			sockaddr_in addr;
			memset (&addr, 0, sizeof (addr));

			addr.sin_family = AF_INET;
			addr.sin_port = htons (options.out_port);

			int r;
			if ((r = inet_pton (AF_INET, options.host, &addr.sin_addr)) != 1) {
				log_error (LOG_DEBUGGER, "Could not setup a socket for stdout and stderr: %s",
						r == -1 ? strerror (errno) : "address not parseable in the specified address family");
				Helpers::abort_application ();
			}

			if (options.server) {
				int accepted = monodroid_debug_accept (sock, addr);
				log_warn (LOG_DEBUGGER, "Accepted stdout connection: %d", accepted);
				if (accepted < 0) {
					log_fatal (LOG_DEBUGGER, "Error accepting stdout and stderr (%s:%d): %s",
							     options.host, options.out_port, strerror (errno));
					Helpers::abort_application ();
				}

				dup2 (accepted, 1);
				dup2 (accepted, 2);
			} else {
				if (monodroid_debug_connect (sock, addr) != 1) {
					log_fatal (LOG_DEBUGGER, "Error connecting stdout and stderr (%s:%d): %s",
							     options.host, options.out_port, strerror (errno));
					Helpers::abort_application ();
				}

				dup2 (sock, 1);
				dup2 (sock, 2);
			}
		}

		if (debug.enable_soft_breakpoints ()) {
			constexpr char soft_breakpoints[] = "--soft-breakpoints";
			debug_options[1] = const_cast<char*> (soft_breakpoints);
			mono_jit_parse_options (2, debug_options);
		} else {
			mono_jit_parse_options (1, debug_options);
		}

		mono_debug_init (MONO_DEBUG_FORMAT_MONO);
	} else {
		set_debug_options ();
	}

	delete[] options.host;
#else
	set_debug_options ();
#endif

	// TESTING ASAN: use-after-free
	// char *x = new char[10]{};
	// delete[] x;
	// log_warn (LOG_DEFAULT, "x == %s", x);

	// TESTING UBSAN: integer overflow
	//log_warn (LOG_DEFAULT, "Let us have an overflow: %d", INT_MAX + 1);

	bool log_methods = FastTiming::enabled () && !FastTiming::is_bare_mode ();
	if (XA_UNLIKELY (log_methods)) {
		std::unique_ptr<char> jit_log_path {utils.path_combine (androidSystem.get_override_dir (0), "methods.txt")};
		jit_log = utils.monodroid_fopen (jit_log_path.get (), "a");
		utils.set_world_accessable (jit_log_path.get ());
	}

	profiler_handle = mono_profiler_create (nullptr);
	mono_profiler_set_thread_started_callback (profiler_handle, thread_start);
	mono_profiler_set_thread_stopped_callback (profiler_handle, thread_end);

	if (XA_UNLIKELY (log_methods)) {
		jit_time.mark_start ();
		mono_profiler_set_jit_begin_callback (profiler_handle, jit_begin);
		mono_profiler_set_jit_done_callback (profiler_handle, jit_done);
		mono_profiler_set_jit_failed_callback (profiler_handle, jit_failed);
	}

	parse_gdb_options ();

	if (wait_for_gdb) {
		log_warn (LOG_DEFAULT, "Waiting for gdb to attach...");
		while (monodroid_gdb_wait) {
			sleep (1);
		}
	}

	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> prop_val;
	/* Additional runtime arguments passed to mono_jit_parse_options () */
	if (androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_RUNTIME_ARGS_PROPERTY, prop_val) > 0) {
		char **ptr;

		log_warn (LOG_DEBUGGER, "passing '%s' as extra arguments to the runtime.\n", prop_val.get ());

		char **args = utils.monodroid_strsplit (prop_val.get (), " ", 0);
		int argc = 0;

		for (ptr = args; *ptr; ptr++)
			argc ++;

		mono_jit_parse_options (argc, args);
	}

	// int argc = 5;
	// char* argv[] = { "-v", "-v", "-v", "-v", "-v" };
	// mono_jit_parse_options (argc, argv);

	mono_set_signal_chaining (1);
	mono_set_crash_chaining (1);

	osBridge.register_gc_hooks ();

	/*
	 * Assembly preload hooks are invoked in _reverse_ registration order.
	 * Looking for assemblies from the update dir takes precedence over
	 * everything else, and thus must go LAST.
	 */
	embeddedAssemblies.install_preload_hooks_for_appdomains ();
#ifndef RELEASE
	mono_install_assembly_preload_hook (open_from_update_dir, nullptr);
#endif

#if defined (RELEASE) && defined (ANDROID) && defined (NET)
	if (application_config.marshal_methods_enabled) {
		xamarin_app_init (env, get_function_pointer_at_startup);
	}
#endif // def RELEASE && def ANDROID && def NET
}

#if defined (NET)
void
MonodroidRuntime::cleanup_runtime_config (MonovmRuntimeConfigArguments *args, [[maybe_unused]] void *user_data)
{
	if (args == nullptr || args->kind != 1 || args->runtimeconfig.data.data == nullptr) {
		return;
	}

#if !defined (WINDOWS)
	munmap (static_cast<void*>(const_cast<char*>(args->runtimeconfig.data.data)), args->runtimeconfig.data.data_len);
#endif // ndef WINDOWS
}
#endif // def NET

MonoDomain*
MonodroidRuntime::create_domain (JNIEnv *env, jstring_array_wrapper &runtimeApks, bool is_root_domain, bool have_split_apks)
{
	size_t user_assemblies_count   = 0;

	gather_bundled_assemblies (runtimeApks, &user_assemblies_count, have_split_apks);

#if defined (NET)
	size_t blob_time_index;
	if (XA_UNLIKELY (FastTiming::enabled ())) {
		blob_time_index = internal_timing->start_event (TimingEventKind::RuntimeConfigBlob);
	}

	if (embeddedAssemblies.have_runtime_config_blob ()) {
		runtime_config_args.kind = 1;
		embeddedAssemblies.get_runtime_config_blob (runtime_config_args.runtimeconfig.data.data, runtime_config_args.runtimeconfig.data.data_len);
		monovm_runtimeconfig_initialize (&runtime_config_args, cleanup_runtime_config, nullptr);
	}

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (blob_time_index);
	}
#endif // def NET

	if (user_assemblies_count == 0 && androidSystem.count_override_assemblies () == 0) {
#if defined (DEBUG)
		log_fatal (LOG_DEFAULT, "No assemblies found in '%s' or '%s'. Assuming this is part of Fast Deployment. Exiting...",
		           androidSystem.get_override_dir (0),
		           (AndroidSystem::MAX_OVERRIDES > 1 && androidSystem.get_override_dir (1) != nullptr) ? androidSystem.get_override_dir (1) : "<unavailable>");
#else
		log_fatal (LOG_DEFAULT, "No assemblies (or assembly shared libraries) were found in the application APK file(s)");
#endif
		log_fatal (LOG_DEFAULT, "Make sure that all entries (if any) in the APK directory named `assemblies/` are STORED (not compressed)");
#if defined (RELEASE)
		log_fatal (LOG_DEFAULT, "Make sure that all the shared libraries (if any) found in the APK directory named lib/%s are STORED, or that they are extracted to the filesystem", SharedConstants::android_apk_abi);
		log_fatal (LOG_DEFAULT, "Check the 'android:extractNativeLibs=' attribute of the 'application' element in your AndroidManifest.xml file to see whether shared libraries are extracted or not");
#endif
		log_fatal (LOG_DEFAULT, "If Android Gradle Plugin's minification feature is enabled, it is likely all the entries in `assemblies/` are compressed");

		Helpers::abort_application ();
	}

	MonoDomain *domain;
#if !defined (NET)
	if (is_root_domain) {
#endif // ndef NET
		domain = mono_jit_init_version (const_cast<char*> ("RootDomain"), const_cast<char*> ("mobile"));
#if !defined (NET)
	} else {
		MonoDomain* root_domain = mono_get_root_domain ();

		constexpr char DOMAIN_NAME[] = "MonoAndroidDomain";
		constexpr size_t DOMAIN_NAME_LENGTH = sizeof(DOMAIN_NAME) - 1;
		constexpr size_t DOMAIN_NAME_TOTAL_SIZE = DOMAIN_NAME_LENGTH + SharedConstants::MAX_INTEGER_DIGIT_COUNT_BASE10;

		static_local_string<DOMAIN_NAME_TOTAL_SIZE + 1> domain_name (DOMAIN_NAME_TOTAL_SIZE);
		domain_name.append (DOMAIN_NAME);
		domain_name.append (android_api_level);

		domain = utils.monodroid_create_appdomain (root_domain, domain_name.get (), /*shadow_copy:*/ 1, /*shadow_directory:*/ androidSystem.get_override_dir (0));
	}
#endif // ndef NET

	if constexpr (is_running_on_desktop) {
		if (is_root_domain) {
			c_unique_ptr<char> corlib_error_message_guard {const_cast<char*>(mono_check_corlib_version ())};
			char *corlib_error_message = corlib_error_message_guard.get ();

			if (corlib_error_message == nullptr) {
				if (!androidSystem.monodroid_get_system_property ("xamarin.studio.fakefaultycorliberrormessage", &corlib_error_message)) {
					corlib_error_message = nullptr;
				}
			}
			if (corlib_error_message != nullptr) {
				jclass ex_klass = env->FindClass ("mono/android/MonoRuntimeException");
				env->ThrowNew (ex_klass, corlib_error_message);
				return nullptr;
			}

			// Load a basic environment for the RootDomain if run on desktop so that we can unload
			// and reload most assemblies including Mono.Android itself
			MonoAssemblyName *aname = mono_assembly_name_new ("System");
			mono_assembly_load_full (aname, nullptr, nullptr, 0);
			mono_assembly_name_free (aname);
		}
	}

	return domain;
}

inline int
MonodroidRuntime::LocalRefsAreIndirect (JNIEnv *env, jclass runtimeClass, int version)
{
	if (version < 14) {
		java_System = nullptr;
		java_System_identityHashCode = 0;
		return 0;
	}

	java_System = utils.get_class_from_runtime_field(env, runtimeClass, "java_lang_System", true);
	java_System_identityHashCode = env->GetStaticMethodID (java_System, "identityHashCode", "(Ljava/lang/Object;)I");

	return 1;
}

force_inline void
MonodroidRuntime::lookup_bridge_info (MonoClass *klass, const OSBridge::MonoJavaGCBridgeType *type, OSBridge::MonoJavaGCBridgeInfo *info)
{
	info->klass             = klass;
	info->handle            = mono_class_get_field_from_name (info->klass, const_cast<char*> ("handle"));
	info->handle_type       = mono_class_get_field_from_name (info->klass, const_cast<char*> ("handle_type"));
	info->refs_added        = mono_class_get_field_from_name (info->klass, const_cast<char*> ("refs_added"));
	info->weak_handle       = mono_class_get_field_from_name (info->klass, const_cast<char*> ("weak_handle"));
	if (info->klass == nullptr || info->handle == nullptr || info->handle_type == nullptr ||
			info->refs_added == nullptr || info->weak_handle == nullptr) {
		log_fatal (LOG_DEFAULT, "The type `%s.%s` is missing required instance fields! handle=%p handle_type=%p refs_added=%p weak_handle=%p",
				type->_namespace, type->_typename,
				info->handle,
				info->handle_type,
				info->refs_added,
				info->weak_handle);
		Helpers::abort_application ();
	}
}

#if defined (NET)
force_inline void
MonodroidRuntime::lookup_bridge_info (MonoImage *image, const OSBridge::MonoJavaGCBridgeType *type, OSBridge::MonoJavaGCBridgeInfo *info)
{
	lookup_bridge_info (
		mono_class_from_name (image, type->_namespace, type->_typename),
		type,
		info
	);
}
#else // def NET
force_inline void
MonodroidRuntime::lookup_bridge_info (MonoDomain *domain, MonoImage *image, const OSBridge::MonoJavaGCBridgeType *type, OSBridge::MonoJavaGCBridgeInfo *info)
{
	lookup_bridge_info (
		utils.monodroid_get_class_from_image (domain, image, type->_namespace, type->_typename),
		type,
		info
	);
}
#endif // ndef NET

#if defined (NET)
void
MonodroidRuntime::monodroid_debugger_unhandled_exception (MonoException *ex)
{
	mono_debugger_agent_unhandled_exception (ex);
}
#endif

void
MonodroidRuntime::init_android_runtime (
#if !defined (NET)
	MonoDomain *domain,
#endif // ndef NET
	JNIEnv *env, jclass runtimeClass, jobject loader)
{
	constexpr char icall_typemap_java_to_managed[] = "Java.Interop.TypeManager::monodroid_typemap_java_to_managed";
	constexpr char icall_typemap_managed_to_java[] = "Android.Runtime.JNIEnv::monodroid_typemap_managed_to_java";

#if defined (RELEASE) && defined (ANDROID)
	// The reason for these using is that otherwise the compiler will complain about not being
	// able to cast overloaded methods to const void* pointers.
	using j2mFn = MonoReflectionType* (*)(MonoString *java_type);
	using m2jFn = const char* (*)(MonoReflectionType *type, const uint8_t *mvid);

	mono_add_internal_call (icall_typemap_java_to_managed, reinterpret_cast<const void*>(static_cast<j2mFn>(EmbeddedAssemblies::typemap_java_to_managed)));
	mono_add_internal_call (icall_typemap_managed_to_java, reinterpret_cast<const void*>(static_cast<m2jFn>(EmbeddedAssemblies::typemap_managed_to_java)));
#else
	mono_add_internal_call (icall_typemap_java_to_managed, reinterpret_cast<const void*>(typemap_java_to_managed));
	mono_add_internal_call (icall_typemap_managed_to_java, reinterpret_cast<const void*>(typemap_managed_to_java));
#endif // def RELEASE && def ANDROID

#if defined (NET)
	mono_add_internal_call ("Android.Runtime.RuntimeNativeMethods::monodroid_debugger_unhandled_exception", reinterpret_cast<const void*> (monodroid_debugger_unhandled_exception));
	mono_add_internal_call ("Android.Runtime.RuntimeNativeMethods::monodroid_unhandled_exception", reinterpret_cast<const void*>(monodroid_unhandled_exception));
#endif // def NET

	struct JnienvInitializeArgs init = {};
	init.javaVm                 = osBridge.get_jvm ();
	init.env                    = env;
	init.logCategories          = log_categories;
	init.version                = env->GetVersion ();
	init.androidSdkVersion      = android_api_level;
	init.localRefsAreIndirect   = LocalRefsAreIndirect (env, runtimeClass, init.androidSdkVersion);
	init.isRunningOnDesktop     = is_running_on_desktop ? 1 : 0;
	init.brokenExceptionTransitions = application_config.broken_exception_transitions ? 1 : 0;
	init.packageNamingPolicy    = static_cast<int>(application_config.package_naming_policy);
	init.boundExceptionType     = application_config.bound_exception_type;
	init.jniAddNativeMethodRegistrationAttributePresent = application_config.jni_add_native_method_registration_attribute_present ? 1 : 0;
	init.jniRemappingInUse = application_config.jni_remapping_replacement_type_count > 0 || application_config.jni_remapping_replacement_method_index_entry_count > 0;
	init.marshalMethodsEnabled  = application_config.marshal_methods_enabled;

	// GC threshold is 90% of the max GREF count
	init.grefGcThreshold        = static_cast<int>(androidSystem.get_gref_gc_threshold ());

	log_warn (LOG_GC, "GREF GC Threshold: %i", init.grefGcThreshold);

	init.grefClass = utils.get_class_from_runtime_field (env, runtimeClass, "java_lang_Class", true);
	Class_getName  = env->GetMethodID (init.grefClass, "getName", "()Ljava/lang/String;");
	init.Class_forName = env->GetStaticMethodID (init.grefClass, "forName", "(Ljava/lang/String;ZLjava/lang/ClassLoader;)Ljava/lang/Class;");

	MonoAssembly *mono_android_assembly;

#if defined (NET)
	mono_android_assembly = utils.monodroid_load_assembly (default_alc, SharedConstants::MONO_ANDROID_ASSEMBLY_NAME);
#else // def NET
	mono_android_assembly = utils.monodroid_load_assembly (domain, SharedConstants::MONO_ANDROID_ASSEMBLY_NAME);
#endif // ndef NET
	MonoImage *mono_android_assembly_image = mono_assembly_get_image (mono_android_assembly);

	uint32_t i = 0;
	for ( ; i < OSBridge::NUM_XA_GC_BRIDGE_TYPES; ++i) {
		lookup_bridge_info (
#if !defined (NET)
			domain,
#endif // ndef NET
			mono_android_assembly_image,
			&osBridge.get_java_gc_bridge_type (i),
			&osBridge.get_java_gc_bridge_info (i)
		);
	}

	MonoClass *runtime;
	MonoMethod *method;

	if constexpr (is_running_on_desktop) {
#if defined (NET)
		runtime = mono_class_from_name (mono_android_assembly_image, SharedConstants::ANDROID_RUNTIME_NS_NAME, SharedConstants::JNIENVINIT_CLASS_NAME);
#else
		runtime = utils.monodroid_get_class_from_image (domain, mono_android_assembly_image, SharedConstants::ANDROID_RUNTIME_NS_NAME, SharedConstants::JNIENVINIT_CLASS_NAME);
#endif // def NET
		method = mono_class_get_method_from_name (runtime, "Initialize", 1);
	} else {
		runtime = mono_class_get (mono_android_assembly_image, application_config.android_runtime_jnienv_class_token);
		method = mono_get_method (mono_android_assembly_image, application_config.jnienv_initialize_method_token, runtime);
	}

	abort_unless (runtime != nullptr, "INTERNAL ERROR: unable to find the Android.Runtime.JNIEnvInit class!");
	abort_unless (method != nullptr, "INTERNAL ERROR: Unable to find the Android.Runtime.JNIEnvInit.Initialize method!");

	MonoAssembly *ji_assm;
#if defined (NET)
	ji_assm = utils.monodroid_load_assembly (default_alc, SharedConstants::JAVA_INTEROP_ASSEMBLY_NAME);
#else // def NET
	ji_assm = utils.monodroid_load_assembly (domain, SharedConstants::JAVA_INTEROP_ASSEMBLY_NAME);
#endif // ndef NET

	MonoImage       *ji_image   = mono_assembly_get_image  (ji_assm);
	for ( ; i < OSBridge::NUM_XA_GC_BRIDGE_TYPES + OSBridge::NUM_JI_GC_BRIDGE_TYPES; ++i) {
		lookup_bridge_info (
#if !defined (NET)
			domain,
#endif // ndef NET
			ji_image,
			&osBridge.get_java_gc_bridge_type (i),
			&osBridge.get_java_gc_bridge_info (i)
		);
	}

	MonoError error;
	/* If running on desktop, we may be swapping in a new Mono.Android image when calling this
	 * so always make sure we have the freshest handle to the method.
	 */
	if (registerType == nullptr || is_running_on_desktop) {
		if constexpr (is_running_on_desktop) {
			registerType = mono_class_get_method_from_name (runtime, "RegisterJniNatives", 5);
		} else {
			registerType = mono_get_method (mono_android_assembly_image, application_config.jnienv_registerjninatives_method_token, runtime);
#if defined (NET) && defined (ANDROID)
			jnienv_register_jni_natives = reinterpret_cast<jnienv_register_jni_natives_fn>(mono_method_get_unmanaged_callers_only_ftnptr (registerType, &error));
#endif // def NET && def ANDROID
		}
	}
	abort_unless (registerType != nullptr, "INTERNAL ERROR: Unable to find Android.Runtime.JNIEnvInit.RegisterJniNatives! %s", mono_error_get_message (&error));

	jclass lrefLoaderClass = env->GetObjectClass (loader);
	init.Loader_loadClass     = env->GetMethodID (lrefLoaderClass, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
	env->DeleteLocalRef (lrefLoaderClass);

	init.grefLoader           = env->NewGlobalRef (loader);
	init.grefIGCUserPeer      = utils.get_class_from_runtime_field (env, runtimeClass, "mono_android_IGCUserPeer", true);

	osBridge.initialize_on_runtime_init (env, runtimeClass);

	log_debug (LOG_DEFAULT, "Calling into managed runtime init");

	size_t native_to_managed_index;
	if (XA_UNLIKELY (FastTiming::enabled ())) {
		native_to_managed_index = internal_timing->start_event (TimingEventKind::NativeToManagedTransition);
	}

#if defined (NET) && defined (ANDROID)
	auto initialize = reinterpret_cast<jnienv_initialize_fn> (mono_method_get_unmanaged_callers_only_ftnptr (method, &error));
	if (initialize == nullptr) {
		log_fatal (LOG_DEFAULT, "Failed to get pointer to Initialize. Mono error: %s", mono_error_get_message (&error));
	}

	abort_unless (
		initialize != nullptr,
		"Failed to obtain unmanaged-callers-only pointer to the Android.Runtime.JNIEnvInit.Initialize method. %s",
		mono_error_get_message (&error)
	);
	initialize (&init);
#else // def NET && def ANDROID
	void *args [] = {
		&init,
	};

	utils.monodroid_runtime_invoke (domain, method, nullptr, args, nullptr);
#endif // ndef NET && ndef ANDROID

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (native_to_managed_index);
	}
}

#if defined (NET)
MonoClass*
MonodroidRuntime::get_android_runtime_class ()
{
	MonoAssembly *assm = utils.monodroid_load_assembly (default_alc, SharedConstants::MONO_ANDROID_ASSEMBLY_NAME);
	MonoImage *image   = mono_assembly_get_image (assm);
	return mono_class_from_name (image, SharedConstants::ANDROID_RUNTIME_NS_NAME, SharedConstants::JNIENV_CLASS_NAME);
}
#else // def NET
MonoClass*
MonodroidRuntime::get_android_runtime_class (MonoDomain *domain)
{
	MonoAssembly *assm = utils.monodroid_load_assembly (domain, SharedConstants::MONO_ANDROID_ASSEMBLY_NAME);
	MonoImage *image   = mono_assembly_get_image (assm);
	return utils.monodroid_get_class_from_image (domain, image, SharedConstants::ANDROID_RUNTIME_NS_NAME, SharedConstants::JNIENV_CLASS_NAME);
}
#endif // ndef NET

inline void
MonodroidRuntime::propagate_uncaught_exception (
#if !defined (NET)
	MonoDomain *domain,
#endif // ndef NET
	JNIEnv *env, jobject javaThread, jthrowable javaException)
{
	MonoClass *runtime;
#if defined (NET)
	runtime = get_android_runtime_class ();
#else
	runtime = get_android_runtime_class (domain);
#endif
	MonoMethod *method = mono_class_get_method_from_name (runtime, "PropagateUncaughtException", 3);

	void* args[] = {
		&env,
		&javaThread,
		&javaException,
	};
#if defined (NET)
	mono_runtime_invoke (method, nullptr, args, nullptr);
#else // def NET
	utils.monodroid_runtime_invoke (domain, method, nullptr, args, nullptr);
#endif // ndef NET
}

#if DEBUG
static void
setup_gc_logging (void)
{
	gc_spew_enabled = androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_GC_PROPERTY, nullptr) > 0;
	if (gc_spew_enabled) {
		log_categories |= LOG_GC;
	}
}
#endif

force_inline unsigned int
MonodroidRuntime::convert_dl_flags (int flags)
{
	unsigned int lflags = (flags & static_cast<int> (MONO_DL_LOCAL))
		? JAVA_INTEROP_LIB_LOAD_LOCALLY
		: JAVA_INTEROP_LIB_LOAD_GLOBALLY;
	return lflags;
}

#if !defined (NET)
force_inline void
MonodroidRuntime::init_internal_api_dso (void *handle)
{
	if (handle == nullptr) {
		log_fatal (LOG_DEFAULT, "Internal API library is required");
		Helpers::abort_application ();
	}

	// There's a very, very small chance of a race condition here, but it should be acceptable and we can save some time
	// by not acquiring the lock on Android systems which don't have the dlopen bug we worked around in
	// https://github.com/xamarin/xamarin-android/pull/4914
	//
	// The race condition will exist only on systems with the above dynamic loader bug and would become a problem only
	// if an application were loading managed assemblies with p/invokes very quickly from different threads. All in all,
	// not a very likely scenario.
	//
	if (handle == api_dso_handle) {
		log_debug (LOG_DEFAULT, "Internal API library already loaded, initialization not necessary");
		return;
	}

	std::lock_guard<std::mutex> lock (api_init_lock);
	if (api_dso_handle != nullptr) {
		auto api_shutdown = reinterpret_cast<external_api_shutdown_fn> (java_interop_lib_symbol (api_dso_handle, MonoAndroidInternalCalls::SHUTDOWN_FUNCTION_NAME, nullptr));
		if (api_shutdown == nullptr) {
			// We COULD ignore this situation, but if the function is missing it means we messed something up and thus
			// it *is* a fatal error.
			log_fatal (LOG_DEFAULT, "Unable to properly close Internal API library, shutdown function '%s' not found in the module", MonoAndroidInternalCalls::SHUTDOWN_FUNCTION_NAME);
			Helpers::abort_application ();
		}
		api_shutdown ();
	}

	api_dso_handle = handle;
	auto api = new MonoAndroidInternalCalls_Impl ();
	auto api_init = reinterpret_cast<external_api_init_fn>(java_interop_lib_symbol (handle, MonoAndroidInternalCalls::INIT_FUNCTION_NAME, nullptr));
	if (api_init == nullptr) {
		log_fatal (LOG_DEFAULT, "Unable to initialize Internal API library, init function '%s' not found in the module", MonoAndroidInternalCalls::INIT_FUNCTION_NAME);
		Helpers::abort_application ();
	}

	log_debug (LOG_DEFAULT, "Initializing Internal API library %p", handle);
	if (!api_init (api)) {
		log_fatal (LOG_DEFAULT, "Failed to initialize Internal API library");
		Helpers::abort_application ();
	}
}
#endif // ndef NET

force_inline DSOCacheEntry*
MonodroidRuntime::find_dso_cache_entry ([[maybe_unused]] hash_t hash) noexcept
{
#if !defined (__MINGW32__) || (defined (__MINGW32__) && __GNUC__ >= 10)
	hash_t entry_hash;
	DSOCacheEntry *ret = nullptr;
	size_t entry_count = application_config.number_of_dso_cache_entries;
	DSOCacheEntry *entries = dso_cache;

	while (entry_count > 0) {
		ret = entries + (entry_count / 2);
		entry_hash = static_cast<hash_t> (ret->hash);
		auto result = hash <=> entry_hash;

		if (result < 0) {
			entry_count /= 2;
		} else if (result > 0) {
			entries = ret + 1;
			entry_count -= entry_count / 2 + 1;
		} else {
			return ret;
		}
	}
#endif // ndef MINGW32 || def MINGW32 && GNUC >= 10
	return nullptr;
}

force_inline void*
MonodroidRuntime::monodroid_dlopen_log_and_return (void *handle, char **err, const char *full_name, bool free_memory, [[maybe_unused]] bool need_api_init)
{
	if (handle == nullptr && err != nullptr) {
		*err = utils.monodroid_strdup_printf ("Could not load library: Library '%s' not found.", full_name);
	}

	if (free_memory) {
		delete[] full_name;
	}

#if !defined (NET)
	if (!need_api_init)
		return handle;
	init_internal_api_dso (handle);
#endif // ndef NET

	return handle;
}

force_inline void*
MonodroidRuntime::monodroid_dlopen_ignore_component_or_load ([[maybe_unused]] hash_t name_hash, const char *name, int flags, char **err) noexcept
{
#if defined (NET)
	if (startup_in_progress) {
		auto ignore_component = [&](const char *label, MonoComponent component) -> bool {
			if ((application_config.mono_components_mask & component) != component) {
				log_info (LOG_ASSEMBLY, "Mono '%s' component requested but not packaged, ignoring", label);
				return true;
			}

			return false;
		};

		switch (name_hash) {
			case mono_component_debugger_hash:
				if (ignore_component ("Debugger", MonoComponent::Debugger)) {
					return nullptr;
				}
				break;

			case mono_component_hot_reload_hash:
				if (ignore_component ("Hot Reload", MonoComponent::HotReload)) {
					return nullptr;
				}
				break;

			case mono_component_diagnostics_tracing_hash:
				if (ignore_component ("Diagnostics Tracing", MonoComponent::Tracing)) {
					return nullptr;
				}
				break;
		}
	}
#endif
	unsigned int dl_flags = monodroidRuntime.convert_dl_flags (flags);
	void * handle = androidSystem.load_dso_from_any_directories (name, dl_flags);
	if (handle != nullptr) {
		return monodroid_dlopen_log_and_return (handle, err, name, false /* name_needs_free */);
	}

	handle = androidSystem.load_dso (name, dl_flags, false /* skip_existing_check */);
	return monodroid_dlopen_log_and_return (handle, err, name, false /* name_needs_free */);
}

force_inline void*
MonodroidRuntime::monodroid_dlopen (const char *name, int flags, char **err) noexcept
{
	hash_t name_hash = xxhash::hash (name, strlen (name));
	log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash for name '%s' is 0x%zx", name, name_hash);
	DSOCacheEntry *dso = find_dso_cache_entry (name_hash);
	log_debug (LOG_ASSEMBLY, "monodroid_dlopen: hash match %sfound, DSO name is '%s'", dso == nullptr ? "not " : "", dso == nullptr ? "<unknown>" : dso->name);

	if (dso == nullptr) {
		// DSO not known at build time, try to load it
		return monodroid_dlopen_ignore_component_or_load (name_hash, name, flags, err);
	} else if (dso->handle != nullptr) {
		return monodroid_dlopen_log_and_return (dso->handle, err, dso->name, false /* name_needs_free */);
	}

	if (dso->ignore) {
		log_info (LOG_ASSEMBLY, "Request to load '%s' ignored, it is known not to exist", dso->name);
		return nullptr;
	}

	StartupAwareLock lock (dso_handle_write_lock);
	unsigned int dl_flags = monodroidRuntime.convert_dl_flags (flags);

	dso->handle = androidSystem.load_dso_from_any_directories (dso->name, dl_flags);
	if (dso->handle != nullptr) {
		return monodroid_dlopen_log_and_return (dso->handle, err, dso->name, false /* name_needs_free */);
	}

	dso->handle = androidSystem.load_dso_from_any_directories (name, dl_flags);
	return monodroid_dlopen_log_and_return (dso->handle, err, name, false /* name_needs_free */);
}

void*
MonodroidRuntime::monodroid_dlopen (const char *name, int flags, char **err, [[maybe_unused]] void *user_data) noexcept
{
	void *h = nullptr;

#if defined (NET)
	if (name == nullptr) {
		log_warn (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+");
		return nullptr;
	}
#else // def NET
	unsigned int dl_flags = monodroidRuntime.convert_dl_flags (flags);

	bool libmonodroid_fallback = false;
	bool name_is_full_path = false;
	bool name_needs_free = false;
	/* name is nullptr when we're P/Invoking __Internal, so remap to libxa-internal-api */
	if (name == nullptr || strstr (name, "xa-internal-api") != nullptr) {
#if defined (WINDOWS)
		char *tmp_name = nullptr;

		auto probe_dll_at = [&](const char *the_path) -> bool {
			if (the_path == nullptr) {
				return false;
			}

			const char *last_sep = strrchr (the_path, MONODROID_PATH_SEPARATOR_CHAR);
			if (last_sep != nullptr) {
				char *dir = utils.strdup_new (the_path, last_sep - the_path);
				if (dir == nullptr) {
					return false;
				}

				tmp_name = utils.string_concat (dir, MONODROID_PATH_SEPARATOR, API_DSO_NAME);
				delete[] dir;
				if (!utils.file_exists (tmp_name)) {
					delete[] tmp_name;
					tmp_name = nullptr;
					return false;
				}

				return true;
			}

			return false;
		};

		//
		// First try to see if it exists at the path pointed to by `name`. With p/invokes, currently (Sep 2020), we can't
		// really trust the path since it consists of *some* directory path + p/invoke library name and it does not
		// point to the location where the native library is. However, we still need to check the location first, should
		// it point to the right place in the future.
		//
		// Context: https://github.com/mono/mono/issues/20295#issuecomment-679271621
		//
		bool found = probe_dll_at (name);
		if (!found) {
			// Next lets try the location of the XA runtime DLL, libxa-internal-api.dll should be next to it.
			const char *path = get_my_location (false);
			found = probe_dll_at (path); // lgtm [cpp/unguardednullreturndereference] probe_dll_at checks whether the passed pointer is nullptr
			if (path != nullptr) {
				free (reinterpret_cast<void*>(const_cast<char*>(path)));
			}

			if (!found) {
				log_warn (LOG_DEFAULT, "Failed to locate %s, using file name without the path", API_DSO_NAME);
				name = API_DSO_NAME;
			} else {
				name = tmp_name;
				name_is_full_path = true;
				name_needs_free = true;
			}
		}
#else // ndef WINDOWS
		name = API_DSO_NAME;
#endif // WINDOWS
		libmonodroid_fallback = true;
	}

	if (!name_is_full_path) {
		// h = androidSystem.load_dso_from_any_directories (name, dl_flags);
		h = monodroid_dlopen (name, flags, err);
		if (h != nullptr) {
			return h; // already logged by monodroid_dlopen
		}
	}

	if (h != nullptr) {
		return monodroid_dlopen_log_and_return (h, err, name, name_needs_free, libmonodroid_fallback);
	}

	if (libmonodroid_fallback) {
		const char *full_name;
		if (name_is_full_path) {
			full_name = name;
		} else {
			if (name_needs_free) {
				delete[] name;
			}
			full_name = utils.path_combine (AndroidSystem::SYSTEM_LIB_PATH, API_DSO_NAME);
			name_needs_free = true;
		}
		h = androidSystem.load_dso (full_name, dl_flags, false);
		return monodroid_dlopen_log_and_return (h, err, full_name, name_needs_free, true);
	}
#endif // ndef NET

	h = monodroid_dlopen (name, flags, err);
#if !defined (NET)
	if (name_needs_free) {
		delete[] name;
	}
#endif // ndef NET
	return h;
}

void*
MonodroidRuntime::monodroid_dlsym (void *handle, const char *name, char **err, [[maybe_unused]] void *user_data)
{
	void *s;
	char *e = nullptr;

	s = java_interop_lib_symbol (handle, name, &e);

	if (!s && err) {
		*err = utils.monodroid_strdup_printf ("Could not find symbol '%s': %s", name, e);
	}
	if (e) {
		java_interop_free (e);
	}

	return s;
}

inline void
MonodroidRuntime::set_environment_variable_for_directory (const char *name, jstring_wrapper &value, bool createDirectory, mode_t mode)
{
	if (createDirectory) {
		int rv = utils.create_directory (value.get_cstr (), mode);
		if (rv < 0 && errno != EEXIST)
			log_warn (LOG_DEFAULT, "Failed to create directory for environment variable %s. %s", name, strerror (errno));
	}
	setenv (name, value.get_cstr (), 1);
}

inline void
MonodroidRuntime::create_xdg_directory (jstring_wrapper& home, size_t home_len, const char *relativePath, size_t relative_path_len, const char *environmentVariableName)
{
	static_local_string<SENSIBLE_PATH_MAX> dir (home_len + relative_path_len);
	utils.path_combine (dir, home.get_cstr (), home_len, relativePath, relative_path_len);
	log_info (LOG_DEFAULT, "Creating XDG directory: %s", dir.get ());
	int rv = utils.create_directory (dir.get (), DEFAULT_DIRECTORY_MODE);
	if (rv < 0 && errno != EEXIST)
		log_warn (LOG_DEFAULT, "Failed to create XDG directory %s. %s", dir.get (), strerror (errno));
	if (environmentVariableName)
		setenv (environmentVariableName, dir.get (), 1);
}

inline void
MonodroidRuntime::create_xdg_directories_and_environment (jstring_wrapper &homeDir)
{
	size_t home_len = strlen (homeDir.get_cstr ());

	constexpr char XDG_DATA_HOME[] = "XDG_DATA_HOME";
	constexpr char HOME_PATH[] = ".local/share";
	constexpr size_t HOME_PATH_LEN = sizeof(HOME_PATH) - 1;
	create_xdg_directory (homeDir, home_len, HOME_PATH, HOME_PATH_LEN, XDG_DATA_HOME);

	constexpr char XDG_CONFIG_HOME[] = "XDG_CONFIG_HOME";
	constexpr char CONFIG_PATH[] = ".config";
	constexpr size_t CONFIG_PATH_LEN = sizeof(CONFIG_PATH) - 1;
	create_xdg_directory (homeDir, home_len, CONFIG_PATH, CONFIG_PATH_LEN, XDG_CONFIG_HOME);
}

#if DEBUG
void
MonodroidRuntime::set_debug_env_vars (void)
{
	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> value;
	if (androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_ENV_PROPERTY, value) == 0)
		return;

	auto log_envvar = [](const char *name, const char *v) {
		log_debug (LOG_DEFAULT, "Env variable '%s' set to '%s'.", name, v);
	};

	string_segment arg_token;
	while (value.next_token ('|', arg_token)) {
		static_local_string<SMALL_STRING_PARSE_BUFFER_LEN> arg (arg_token.length ());
		arg.assign (arg_token.start (), arg_token.length ());

		ssize_t idx = arg.index_of ('=');
		size_t index = static_cast<size_t>(idx);
		if (idx < 0 || index == arg.length () - 1) {
			// ’name’ or ’name=’
			constexpr char one[] = "1";
			if (index == arg.length () - 1) {
				arg[index] = '\0';
			}
			setenv (arg.get (), one, 1);
			log_envvar (arg.get (), one);
		} else if (index == 0) {
			// ’=value’
			log_warn (LOG_DEFAULT, "Attempt to set environment variable without specifying name: '%s'", arg.get ());
		} else {
			// ’name=value’
			arg[index] = '\0';
			const char *v = arg.get () + idx + 1;
			setenv (arg.get (), v, 1);
			log_envvar (arg.get (), v);
		}
	}
}
#endif /* DEBUG */

inline void
MonodroidRuntime::set_trace_options (void)
{
	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> value;
	if (androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_TRACE_PROPERTY, value) == 0)
		return;

	mono_jit_set_trace_options (value.get ());
}

#if defined (NET)
inline void
MonodroidRuntime::set_profile_options ()
{
	// We want to avoid dynamic allocation, thus let’s create a buffer that can take both the property value and a
	// path without allocation
	dynamic_local_string<SENSIBLE_PATH_MAX + PROPERTY_VALUE_BUFFER_LEN> value;
	{
		dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> prop_value;
		if (androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_PROFILE_PROPERTY, prop_value) == 0)
			return;

		value.assign (prop_value);
	}

	// NET+ supports only the AOT Mono profiler, if the prefix is absent or different than 'aot:' we consider the
	// property to contain value for the dotnet tracing profiler.
	constexpr char AOT_PREFIX[] = "aot:";
	if (!value.starts_with (AOT_PREFIX)) {
		// setenv(3) makes copies of its arguments
		setenv ("DOTNET_DiagnosticPorts", value.get (), 1);
		return;
	}

	constexpr char OUTPUT_ARG[] = "output=";
	constexpr size_t OUTPUT_ARG_LEN = sizeof(OUTPUT_ARG) - 1;
	constexpr size_t start_index = sizeof(AOT_PREFIX); // one char past ':'

	dynamic_local_string<SENSIBLE_PATH_MAX> output_path;
	bool have_output_arg = false;
	string_segment param;

	while (value.next_token (start_index, ',', param)) {
		dynamic_local_string<SENSIBLE_PATH_MAX> temp;
		temp.assign (param.start (), param.length ());
		if (!param.starts_with (OUTPUT_ARG)) {
			continue;
		}

		output_path.assign (param.start () + OUTPUT_ARG_LEN, param.length () - OUTPUT_ARG_LEN);
		have_output_arg = true;
		break;
	}

	if (!have_output_arg) {
		constexpr char PROFILE_FILE_NAME_PREFIX[] = "profile.";
		constexpr char AOT_EXT[] = "aotprofile";

		output_path
			.assign_c (androidSystem.get_override_dir (0))
			.append (MONODROID_PATH_SEPARATOR)
			.append (PROFILE_FILE_NAME_PREFIX)
			.append (AOT_EXT);

		value
			.append (",")
			.append (OUTPUT_ARG)
			.append (output_path.get (), output_path.length ());
	}

	log_warn (LOG_DEFAULT, "Initializing profiler with options: %s", value.get ());
	debug.monodroid_profiler_load (androidSystem.get_runtime_libdir (), value.get (), output_path.get ());
}
#else // def NET
inline void
MonodroidRuntime::set_profile_options ()
{
	// We want to avoid dynamic allocation, thus let’s create a buffer that can take both the property value and a
	// path without allocation
	dynamic_local_string<SENSIBLE_PATH_MAX + PROPERTY_VALUE_BUFFER_LEN> value;
	{
		dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> prop_value;
		if (androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_PROFILE_PROPERTY, prop_value) == 0)
			return;

		value.assign (prop_value.get (), prop_value.length ());
	}

	constexpr char OUTPUT_ARG[] = "output=";
	constexpr size_t OUTPUT_ARG_LEN = sizeof(OUTPUT_ARG) - 1;

	ssize_t colon_idx = value.index_of (':');
	size_t start_index = colon_idx < 0 ? 0 : static_cast<size_t>(colon_idx + 1);
	dynamic_local_string<SENSIBLE_PATH_MAX> output_path;
	bool have_output_arg = false;
	string_segment param;

	while (value.next_token (start_index, ',', param)) {
		dynamic_local_string<SENSIBLE_PATH_MAX> temp;
		temp.assign (param.start (), param.length ());
		if (!param.starts_with (OUTPUT_ARG) || param.length () == OUTPUT_ARG_LEN) {
			continue;
		}

		output_path.assign (param.start () + OUTPUT_ARG_LEN, param.length () - OUTPUT_ARG_LEN);
		have_output_arg = true;
		break;
	}

	if (!have_output_arg) {
		constexpr char   MLPD_EXT[] = "mlpd";
		constexpr char   AOT_EXT[] = "aotprofile";
		constexpr char   COV_EXT[] = "xml";
		constexpr char   LOG_PREFIX[] = "log:";
		constexpr size_t LOG_PREFIX_LENGTH = sizeof(LOG_PREFIX) - 1;
		constexpr char   AOT_PREFIX[] = "aot:";
		constexpr size_t AOT_PREFIX_LENGTH = sizeof(AOT_PREFIX) - 1;
		constexpr char   DEFAULT_PREFIX[] = "default:";
		constexpr size_t DEFAULT_PREFIX_LENGTH = sizeof(DEFAULT_PREFIX) - 1;
		constexpr char   COVERAGE_PREFIX[] = "coverage:";
		constexpr size_t COVERAGE_PREFIX_LENGTH = sizeof(COVERAGE_PREFIX) - 1;
		constexpr char   PROFILE_FILE_NAME_PREFIX[] = "profile.";

		size_t length_adjust = colon_idx >= 1 ? 0 : 1;

		output_path
			.assign_c (androidSystem.get_override_dir (0))
			.append (MONODROID_PATH_SEPARATOR)
			.append (PROFILE_FILE_NAME_PREFIX);

		if (value.starts_with (LOG_PREFIX, LOG_PREFIX_LENGTH - length_adjust)) {
			output_path.append (MLPD_EXT);
		} else if (value.starts_with (AOT_PREFIX, AOT_PREFIX_LENGTH - length_adjust)) {
			output_path.append (AOT_EXT);
		} else if (value.starts_with (DEFAULT_PREFIX, DEFAULT_PREFIX_LENGTH - length_adjust)) {
			output_path.append (MLPD_EXT);
		} else if (value.starts_with (COVERAGE_PREFIX, COVERAGE_PREFIX_LENGTH - length_adjust)) {
			output_path.append (COV_EXT);
		} else {
			size_t len = colon_idx < 0 ? value.length () : static_cast<size_t>(colon_idx + 1);
			output_path.append (value.get (), len);
		}

		if (colon_idx < 0)
			value.append (":");
		else
			value.append (",");
		value
			.append (OUTPUT_ARG)
			.append (output_path.get (), output_path.length ());
	}

	/*
	 * libmono-profiler-log.so profiler won't overwrite existing files.
	 * Remove it For Great Justice^H^H^H to preserve my sanity!
	 */
	unlink (output_path.get ());

	log_warn (LOG_DEFAULT, "Initializing profiler with options: %s", value.get ());
	debug.monodroid_profiler_load (androidSystem.get_runtime_libdir (), value.get (), output_path.get ());
}
#endif // ndef NET

/*
Disable LLVM signal handlers.

This happens when RenderScript needs to be compiled. See https://bugzilla.xamarin.com/show_bug.cgi?id=18016

This happens only on first run of the app. LLVM is used to compiled the RenderScript scripts. LLVM, been
a nice and smart library installs a ton of signal handlers and don't chain at all, completely breaking us.

This is a hack to set llvm::DisablePrettyStackTrace to true and avoid this source of signal handlers.

As of Android 5.0 (API 21) the symbol no longer exists in libLLVM.so and stack pretty printing is an opt-in
instead of an opt-out feature. LLVM change which removed the symbol is at

https://github.com/llvm/llvm-project/commit/c10ca903243f97cbc8014f20c64f1318a57a2936

*/
void
MonodroidRuntime::disable_external_signal_handlers (void)
{
#if !defined (NET)
	if (android_api_level >= 21) {
		return;
	}

	void *llvm  = androidSystem.load_dso ("libLLVM.so", JAVA_INTEROP_LIB_LOAD_GLOBALLY, TRUE);
	if (llvm) {
		bool *disable_signals = reinterpret_cast<bool*> (java_interop_lib_symbol (llvm, "_ZN4llvm23DisablePrettyStackTraceE", nullptr));
		if (disable_signals) {
			*disable_signals = true;
			log_info (LOG_DEFAULT, "Disabled LLVM signal trapping");
		}
		//MUST NOT dlclose to ensure we don't lose the hack
	}
#endif // ndef NET
}

#if defined (NET)
inline void
MonodroidRuntime::load_assembly (MonoAssemblyLoadContextGCHandle alc_handle, jstring_wrapper &assembly)
{
	size_t total_time_index;
	if (XA_UNLIKELY (FastTiming::enabled ())) {
		total_time_index = internal_timing->start_event (TimingEventKind::AssemblyLoad);
	}

	const char *assm_name = assembly.get_cstr ();
	if (XA_UNLIKELY (assm_name == nullptr)) {
		log_warn (LOG_ASSEMBLY, "Unable to load assembly into ALC, name is null");
		return;
	}

	MonoAssemblyName *aname = mono_assembly_name_new (assm_name);

	MonoImageOpenStatus open_status;
	mono_assembly_load_full_alc (alc_handle, aname, nullptr /* basedir */, &open_status);

	mono_assembly_name_free (aname);

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (total_time_index, true /* uses_more_info */);

		constexpr char PREFIX[] = " (ALC): ";
		constexpr size_t PREFIX_SIZE = sizeof(PREFIX) - 1;

		dynamic_local_string<SENSIBLE_PATH_MAX + PREFIX_SIZE> more_info { PREFIX };
		more_info.append_c (assm_name);
		internal_timing->add_more_info (total_time_index, more_info);
	}
}
#endif // NET

inline void
MonodroidRuntime::load_assembly (MonoDomain *domain, jstring_wrapper &assembly)
{
	size_t total_time_index;
	if (XA_UNLIKELY (FastTiming::enabled ())) {
		total_time_index = internal_timing->start_event (TimingEventKind::AssemblyLoad);
	}

	const char *assm_name = assembly.get_cstr ();
	if (XA_UNLIKELY (assm_name == nullptr)) {
		log_warn (LOG_ASSEMBLY, "Unable to load assembly into AppDomain, name is null");
		return;
	}

	MonoAssemblyName *aname = mono_assembly_name_new (assm_name);

#ifndef ANDROID
	if (designerAssemblies.has_assemblies () && designerAssemblies.try_load_assembly (domain, aname) != nullptr) {
		log_debug (LOG_ASSEMBLY, "Dynamically opened assembly %s", mono_assembly_name_get_name (aname));
	} else
#endif
	{
		MonoDomain *current = utils.get_current_domain ();
		if (domain != current) {
			mono_domain_set (domain, FALSE);
			mono_assembly_load_full (aname, NULL, NULL, 0);
			mono_domain_set (current, FALSE);
		} else {
			mono_assembly_load_full (aname, NULL, NULL, 0);
		}
	}

	mono_assembly_name_free (aname);

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (total_time_index, true /* uses_more_info */);

		constexpr char PREFIX[] = " (domain): ";
		constexpr size_t PREFIX_SIZE = sizeof(PREFIX) - 1;

		dynamic_local_string<SENSIBLE_PATH_MAX + PREFIX_SIZE> more_info { PREFIX };
		more_info.append_c (assm_name);
		internal_timing->add_more_info (total_time_index, more_info);
	}
}

inline void
MonodroidRuntime::load_assemblies (load_assemblies_context_type ctx, bool preload, jstring_array_wrapper &assemblies)
{
	size_t total_time_index;
	if (XA_UNLIKELY (FastTiming::enabled ())) {
		total_time_index = internal_timing->start_event (TimingEventKind::AssemblyPreload);
	}

	size_t i = 0;
	for (i = 0; i < assemblies.get_length (); ++i) {
		jstring_wrapper &assembly = assemblies [i];
		load_assembly (ctx, assembly);
		// only load the first "main" assembly if we are not preloading.
		if (!preload)
			break;
	}

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (total_time_index, true /* uses-more_info */);

		static_local_string<SharedConstants::INTEGER_BASE10_BUFFER_SIZE> more_info;
		more_info.append (static_cast<uint64_t>(i + 1));
		internal_timing->add_more_info (total_time_index, more_info);
	}
}

[[maybe_unused]] static void
monodroid_Mono_UnhandledException_internal ([[maybe_unused]] MonoException *ex)
{
	// Do nothing with it here, we let the exception naturally propagate on the managed side
}

MonoDomain*
MonodroidRuntime::create_and_initialize_domain (JNIEnv* env, jclass runtimeClass, jstring_array_wrapper &runtimeApks,
                                                jstring_array_wrapper &assemblies, [[maybe_unused]] jobjectArray assembliesBytes,
                                                [[maybe_unused]] jstring_array_wrapper &assembliesPaths, jobject loader, bool is_root_domain,
                                                bool force_preload_assemblies, bool have_split_apks)
{
	MonoDomain* domain = create_domain (env, runtimeApks, is_root_domain, have_split_apks);
#if defined (ANDROID)
	// Asserting this on desktop apparently breaks a Designer test
	abort_unless (domain != nullptr, "Failed to create AppDomain");
#endif

	// When running on desktop, the root domain is only a dummy so don't initialize it
	if constexpr (is_running_on_desktop) {
		if (is_root_domain) {
			return domain;
		}
	}

#if defined (NET)
	default_alc = mono_alc_get_default_gchandle ();
	abort_unless (default_alc != nullptr, "Default AssemblyLoadContext not found");

	embeddedAssemblies.install_preload_hooks_for_alc ();
	log_debug (LOG_ASSEMBLY, "ALC hooks installed");
#endif // def NET

#ifndef ANDROID
	if (assembliesBytes != nullptr && domain != nullptr)
		designerAssemblies.add_or_update_from_java (domain, env, assemblies, assembliesBytes, assembliesPaths);
#endif
	bool preload = (androidSystem.is_assembly_preload_enabled () || (is_running_on_desktop && force_preload_assemblies));

#if defined (NET)
	load_assemblies (default_alc, preload, assemblies);
	init_android_runtime (env, runtimeClass, loader);
#else // def NET
	load_assemblies (domain, preload, assemblies);
	init_android_runtime (domain, env, runtimeClass, loader);
#endif // ndef NET
	osBridge.add_monodroid_domain (domain);

	return domain;
}

#if defined (NET)
void
MonodroidRuntime::monodroid_unhandled_exception (MonoObject *java_exception)
{
	mono_unhandled_exception (java_exception);
}
#endif // def NET

#if !defined (RELEASE) || !defined (ANDROID)
MonoReflectionType*
MonodroidRuntime::typemap_java_to_managed (MonoString *java_type_name) noexcept
{
	return embeddedAssemblies.typemap_java_to_managed (java_type_name);
}

const char*
MonodroidRuntime::typemap_managed_to_java (MonoReflectionType *type, const uint8_t *mvid) noexcept
{
	return embeddedAssemblies.typemap_managed_to_java (type, mvid);
}
#endif // !def RELEASE || !def ANDROID

#if defined (WINDOWS)
const char*
MonodroidRuntime::get_my_location (bool remove_file_name)
{
	HMODULE hm = NULL;

	DWORD handle_flags = GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT;
	if (GetModuleHandleExW (handle_flags, (LPCWSTR) &get_xamarin_android_msbuild_path, &hm) == 0) {
		int ret = GetLastError ();
		log_warn (LOG_DEFAULT, "Unable to get HANDLE to `libmono-android.debug.dll`; GetModuleHandleExW returned %d\n", ret);
		return nullptr;
	}

	WCHAR path[MAX_PATH * 2];
	if (GetModuleFileNameW (hm, path, sizeof(path)) == 0) {
		int ret = GetLastError ();
		log_warn (LOG_DEFAULT, "Unable to get filename to `libmono-android.debug.dll`; GetModuleFileNameW returned %d\n", ret);
		return nullptr;
	}

	if (remove_file_name)
		PathRemoveFileSpecW (path);

	return utils.utf16_to_utf8 (path);
}
#elif defined (APPLE_OS_X)
const char*
MonodroidRuntime::get_my_location (bool remove_file_name)
{
	Dl_info info;
	if (dladdr (reinterpret_cast<const void*>(&MonodroidRuntime::get_my_location), &info) == 0) {
		log_warn (LOG_DEFAULT, "Could not lookup library containing `MonodroidRuntime::get_my_location()`; dladdr failed: %s", dlerror ());
		return nullptr;
	}

	if (remove_file_name)
		return utils.strdup_new (dirname (const_cast<char*>(info.dli_fname)));
	return utils.strdup_new (info.dli_fname);
}
#endif  // defined(WINDOWS)

#if defined (ANDROID)
force_inline void
MonodroidRuntime::setup_mono_tracing (std::unique_ptr<char[]> const& mono_log_mask, bool have_log_assembly, bool have_log_gc)
{
	constexpr char   MASK_ASM[] = "asm";
	constexpr size_t MASK_ASM_LEN = sizeof(MASK_ASM) - 1;
	constexpr char   MASK_DLL[] = "dll";
	constexpr size_t MASK_DLL_LEN = sizeof(MASK_DLL) - 1;
	constexpr char   MASK_GC[] = "gc";
	constexpr size_t MASK_GC_LEN = sizeof(MASK_GC) - 1;
	constexpr char   COMMA[] = ",";

	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> log_mask;
	if (mono_log_mask == nullptr || *mono_log_mask.get () == '\0') {
		if (have_log_assembly) {
			log_mask.append (MASK_ASM);
			log_mask.append (COMMA);
			log_mask.append (MASK_DLL);
		}

		if (have_log_gc) {
			if (log_mask.length () > 0) {
				log_mask.append (COMMA);
			}
			log_mask.append (MASK_GC);
		}

		// empty string turns off all Mono VM tracing
		mono_trace_set_mask_string (log_mask.get ());
		return;
	}

	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> input_log_mask;
	input_log_mask.assign_c (mono_log_mask.get ());
	input_log_mask.replace (':', ',');

	if (!have_log_assembly && !have_log_gc)  {
		mono_trace_set_mask_string (input_log_mask.get ());
		return;
	}

	bool need_asm = have_log_assembly, need_dll = have_log_assembly, need_gc = have_log_gc;

	string_segment token;
	while (input_log_mask.next_token (',', token)) {
		if (need_asm && token.equal (MASK_ASM, MASK_ASM_LEN)) {
			need_asm = false;
		} else if (need_dll && token.equal (MASK_DLL, MASK_DLL_LEN)) {
			need_dll = false;
		} else if (need_gc && token.equal (MASK_GC, MASK_GC_LEN)) {
			need_gc = false;
		}

		if (!need_asm && !need_dll && !need_gc) {
			mono_trace_set_mask_string (input_log_mask.get ());
			return;
		}
	}

	if (need_asm) {
		input_log_mask.append (COMMA);
		input_log_mask.append (MASK_ASM);
	}

	if (need_dll) {
		input_log_mask.append (COMMA);
		input_log_mask.append (MASK_DLL);
	}

	if (need_gc) {
		input_log_mask.append (COMMA);
		input_log_mask.append (MASK_GC);
	}

	mono_trace_set_mask_string (input_log_mask.get ());
}

force_inline void
MonodroidRuntime::install_logging_handlers ()
{
	mono_trace_set_log_handler (mono_log_handler, nullptr);
	mono_trace_set_print_handler (mono_log_standard_streams_handler);
	mono_trace_set_printerr_handler (mono_log_standard_streams_handler);
}

#endif // def ANDROID

inline void
MonodroidRuntime::Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
                                                          jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset,
                                                          jobject loader, jobjectArray assembliesJava, jint apiLevel, jboolean isEmulator,
                                                          jboolean haveSplitApks)
{
	char *mono_log_mask_raw = nullptr;
	char *mono_log_level_raw = nullptr;

	init_logging_categories (mono_log_mask_raw, mono_log_level_raw);

	std::unique_ptr<char[]> mono_log_mask (mono_log_mask_raw);
	std::unique_ptr<char[]> mono_log_level (mono_log_level_raw);

	// If fast logging is disabled, log messages immediately
	FastTiming::initialize ((log_timing_categories & LOG_TIMING_FAST_BARE) == 0);

	size_t total_time_index;
	if (XA_UNLIKELY (FastTiming::enabled ())) {
		timing = new Timing ();
		total_time_index = internal_timing->start_event (TimingEventKind::TotalRuntimeInit);
	}

	jstring_array_wrapper applicationDirs (env, appDirs);
	jstring_wrapper &home = applicationDirs[SharedConstants::APP_DIRS_FILES_DIR_INDEX];

#if defined (NET)
	mono_opt_aot_lazy_assembly_load = application_config.aot_lazy_load ? TRUE : FALSE;

	{
		MonoVMProperties monovm_props { home, localDateTimeOffset };

		// NOTE: the `const_cast` breaks the contract made to MonoVMProperties that the arrays it returns won't be
		// modified, but it's "ok" since Mono doesn't modify them and by using `const char* const*` in MonoVMProperties
		// we may get better code generated (since the methods returning the arrays are marked as `const`, thus not
		// modifying the class state, allowing the compiler to make some assumptions when optimizing) and the class
		// itself doesn't touch the arrays outside its constructor.
		monovm_initialize_preparsed (
			&monovm_core_properties,
			monovm_props.property_count (),
			const_cast<const char**>(monovm_props.property_keys ()),
			const_cast<const char**>(monovm_props.property_values ())
		);
	}
#endif // def NET

	android_api_level = apiLevel;
	androidSystem.detect_embedded_dso_mode (applicationDirs);
	androidSystem.set_running_in_emulator (isEmulator);

	java_TimeZone = utils.get_class_from_runtime_field (env, klass, "java_util_TimeZone", true);

	utils.monodroid_store_package_name (application_config.android_package_name);

	jstring_wrapper jstr (env, lang);
	set_environment_variable ("LANG", jstr);

	androidSystem.setup_environment ();

	set_environment_variable_for_directory ("TMPDIR", applicationDirs[SharedConstants::APP_DIRS_CACHE_DIR_INDEX]);
	set_environment_variable_for_directory ("HOME", home);
	create_xdg_directories_and_environment (home);
	androidSystem.set_primary_override_dir (home);

	disable_external_signal_handlers ();

	jstring_array_wrapper runtimeApks (env, runtimeApksJava);
	androidSystem.setup_app_library_directories (runtimeApks, applicationDirs, haveSplitApks);

	init_reference_logging (androidSystem.get_primary_override_dir ());
	androidSystem.create_update_dir (androidSystem.get_primary_override_dir ());

#if DEBUG
	setup_gc_logging ();
	set_debug_env_vars ();
#endif

#if defined (ANDROID)
	bool have_log_assembly = (log_categories & LOG_ASSEMBLY) != 0;
	bool have_log_gc = (log_categories & LOG_GC) != 0;

	if (mono_log_level == nullptr || *mono_log_level.get () == '\0') {
		mono_trace_set_level_string ((have_log_assembly || have_log_gc) ? "info" : "error");
	} else {
		mono_trace_set_level_string (mono_log_level.get ());
	}

	setup_mono_tracing (mono_log_mask, have_log_assembly, have_log_gc);

#if defined (NET)
	install_logging_handlers ();
#endif // def NET
#endif

	if (runtimeNativeLibDir != nullptr) {
		jstr = runtimeNativeLibDir;
		androidSystem.set_runtime_libdir (strdup (jstr.get_cstr ()));
		log_info (LOG_DEFAULT, "Using runtime path: %s", androidSystem.get_runtime_libdir ());
	}

	androidSystem.setup_process_args (runtimeApks);
#if !defined (NET)
	// JIT stats based on perf counters are disabled in dotnet/mono
	if (XA_UNLIKELY (FastTiming::enabled () && !FastTiming::is_bare_mode ())) {
		mono_counters_enable (static_cast<int>(XA_LOG_COUNTERS));

		dynamic_local_string<SENSIBLE_PATH_MAX> counters_path;
		utils.path_combine (counters_path, androidSystem.get_override_dir (0), "counters.txt");
		log_info_nocheck (LOG_TIMING, "counters path: %s", counters_path.get ());
		counters = utils.monodroid_fopen (counters_path.get (), "a");
		utils.set_world_accessable (counters_path.get ());
	}

	void *dso_handle = nullptr;
#if defined (WINDOWS) || defined (APPLE_OS_X)
	const char *my_location = get_my_location ();
	if (my_location != nullptr) {
		std::unique_ptr<char[]> dso_path {utils.path_combine (my_location, API_DSO_NAME)};
		log_info (LOG_DEFAULT, "Attempting to load %s", dso_path.get ());
		dso_handle = java_interop_lib_load (dso_path.get (), JAVA_INTEROP_LIB_LOAD_GLOBALLY, nullptr);
#if defined (APPLE_OS_X)
		delete[] my_location;
#else   // !defined(APPLE_OS_X)
		free (static_cast<void*>(const_cast<char*>(my_location))); // JI allocates with `calloc`
#endif  // defined(APPLE_OS_X)
	}

	if (dso_handle == nullptr) {
		log_info (LOG_DEFAULT, "Attempting to load %s with \"bare\" dlopen", API_DSO_NAME);
		dso_handle = java_interop_lib_load (API_DSO_NAME, JAVA_INTEROP_LIB_LOAD_GLOBALLY, nullptr);
	}
#endif  // defined(WINDOWS) || defined(APPLE_OS_X)
	if (dso_handle == nullptr)
		dso_handle = androidSystem.load_dso_from_any_directories (API_DSO_NAME, JAVA_INTEROP_LIB_LOAD_GLOBALLY);

	init_internal_api_dso (dso_handle);
#endif // ndef NET
	mono_dl_fallback_register (monodroid_dlopen, monodroid_dlsym, nullptr, nullptr);

	set_profile_options ();

	set_trace_options ();

#if defined (DEBUG) && !defined (WINDOWS)
	debug.start_debugging_and_profiling ();
#endif

#if !defined (NET)
	mono_config_parse_memory (reinterpret_cast<const char*> (monodroid_config));
	mono_register_machine_config (reinterpret_cast<const char*> (monodroid_machine_config));
#endif // ndef NET
	log_info (LOG_DEFAULT, "Probing for Mono AOT mode\n");

	MonoAotMode mode = MonoAotMode::MONO_AOT_MODE_NONE;
	if (androidSystem.is_mono_aot_enabled ()) {
		mode = androidSystem.get_mono_aot_mode ();
#if !defined (NET)
		if (mode == MonoAotMode::MONO_AOT_MODE_LAST) {
			// Hack. See comments in android-system.hh
			if (!androidSystem.is_interpreter_enabled ()) {
				mode = MonoAotMode::MONO_AOT_MODE_NONE;
			}
		}

		if (mode != MonoAotMode::MONO_AOT_MODE_NONE) {
			if (mode != MonoAotMode::MONO_AOT_MODE_LAST) {
				log_info (LOG_DEFAULT, "Enabling AOT mode in Mono");
			} else {
				log_info (LOG_DEFAULT, "Enabling Mono Interpreter");
			}
		}
#else   // defined (NET)
		if (mode != MonoAotMode::MONO_AOT_MODE_INTERP_ONLY) {
			log_info (LOG_DEFAULT, "Enabling AOT mode in Mono");
		} else {
			log_info (LOG_DEFAULT, "Enabling Mono Interpreter");
		}
#endif  // !defined (NET)
	}
	mono_jit_set_aot_mode (mode);

	log_info (LOG_DEFAULT, "Probing if we should use LLVM\n");

	if (androidSystem.is_mono_llvm_enabled ()) {
		char *args [1];
		args[0] = const_cast<char*> ("--llvm");
		log_info (LOG_DEFAULT, "Enabling LLVM mode in Mono\n");
		mono_jit_parse_options (1,  args);
		mono_set_use_llvm (true);
	}

	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> runtime_args;
	androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_EXTRA_PROPERTY, runtime_args);

#if TRACE
	__android_log_print (ANDROID_LOG_INFO, "*jonp*", "debug.mono.extra=%s", runtime_args);
#endif

	size_t mono_runtime_init_index;
	if (XA_UNLIKELY (FastTiming::enabled ())) {
		mono_runtime_init_index = internal_timing->start_event (TimingEventKind::MonoRuntimeInit);
	}

	mono_runtime_init (env, runtime_args);

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (mono_runtime_init_index);
	}

	jstring_array_wrapper assemblies (env, assembliesJava);
	jstring_array_wrapper assembliesPaths (env);
	/* the first assembly is used to initialize the AppDomain name */
	create_and_initialize_domain (env, klass, runtimeApks, assemblies, nullptr, assembliesPaths, loader, /*is_root_domain:*/ true, /*force_preload_assemblies:*/ false, haveSplitApks);

#if defined (ANDROID) && !defined (NET)
	// Mono from mono/mono has a bug which requires us to install the handlers after `mono_init_jit_version` is called
	install_logging_handlers ();
#endif // def ANDROID && ndef NET

	// Install our dummy exception handler on Desktop
	if constexpr (is_running_on_desktop) {
		mono_add_internal_call ("System.Diagnostics.Debugger::Mono_UnhandledException_internal(System.Exception)",
		                                 reinterpret_cast<const void*> (monodroid_Mono_UnhandledException_internal));
	}

	if (XA_UNLIKELY (utils.should_log (LOG_DEFAULT))) {
		log_info_nocheck (
			LOG_DEFAULT,
			"Xamarin.Android version: %s (%s; %s); built on %s",
			BuildInfo::xa_version,
			BuildInfo::architecture,
			BuildInfo::kind,
			BuildInfo::date
		);

#if defined (ANDROID)
		log_info_nocheck (LOG_DEFAULT, "NDK version: %s; API level: %s", BuildInfo::ndk_version, BuildInfo::ndk_api_level);
		log_info_nocheck (LOG_DEFAULT, "MonoVM version: %s", mono_get_runtime_build_info ());
#endif // def ANDROID
	}

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (total_time_index);
#if !defined (NET)
		dump_counters ("## Runtime.init: end");
#endif // ndef NET
	}

#if defined (RELEASE) && defined (ANDROID) && defined (NET)
	if (application_config.marshal_methods_enabled) {
		xamarin_app_init (env, get_function_pointer_at_runtime);
	}
#endif // def RELEASE && def ANDROID && def NET
	startup_in_progress = false;
}

#if !defined (NET)
void
MonodroidRuntime::dump_counters (const char *format, ...)
{
	if (counters == nullptr)
		return;

	va_list args;
	va_start (args, format);
	dump_counters_v (format, args);
	va_end (args);
}

void
MonodroidRuntime::dump_counters_v (const char *format, va_list args)
{
	if (counters == nullptr)
		return;

	fprintf (counters, "\n");
	vfprintf (counters, format, args);
	fprintf (counters, "\n");

	mono_counters_dump (MonodroidRuntime::XA_LOG_COUNTERS, counters);
}
#endif // ndef NET

JNIEXPORT jint JNICALL
JNI_OnLoad (JavaVM *vm, void *reserved)
{
	return monodroidRuntime.Java_JNI_OnLoad (vm, reserved);
}

/* !DO NOT REMOVE! Used by the Android Designer */
JNIEXPORT void JNICALL
Java_mono_android_Runtime_init (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
                                jstring runtimeNativeLibDir, jobjectArray appDirs, jobject loader,
                                [[maybe_unused]] jobjectArray externalStorageDirs, jobjectArray assembliesJava, [[maybe_unused]] jstring packageName,
                                jint apiLevel, [[maybe_unused]] jobjectArray environmentVariables)
{
	monodroidRuntime.Java_mono_android_Runtime_initInternal (
		env,
		klass,
		lang,
		runtimeApksJava,
		runtimeNativeLibDir,
		appDirs,
		0,
		loader,
		assembliesJava,
		apiLevel,
		/* isEmulator */ JNI_FALSE,
		/* haveSplitApks */ JNI_FALSE
	);
}

JNIEXPORT void JNICALL
Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
                                jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset, jobject loader,
                                jobjectArray assembliesJava, jint apiLevel, jboolean isEmulator,
                                jboolean haveSplitApks)
{
	monodroidRuntime.Java_mono_android_Runtime_initInternal (
		env,
		klass,
		lang,
		runtimeApksJava,
		runtimeNativeLibDir,
		appDirs,
		localDateTimeOffset,
		loader,
		assembliesJava,
		apiLevel,
		isEmulator,
		haveSplitApks
	);
}

force_inline void
MonodroidRuntime::Java_mono_android_Runtime_register (JNIEnv *env, jstring managedType, jclass nativeClass, jstring methods)
{
	size_t total_time_index;

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		total_time_index = internal_timing->start_event (TimingEventKind::RuntimeRegister);
	}

	jsize managedType_len = env->GetStringLength (managedType);
	const jchar *managedType_ptr = env->GetStringChars (managedType, nullptr);
	int methods_len = env->GetStringLength (methods);
	const jchar *methods_ptr = env->GetStringChars (methods, nullptr);

#if !defined (NET) || !defined (ANDROID)
	void *args[] = {
		&managedType_ptr,
		&managedType_len,
		&nativeClass,
		&methods_ptr,
		&methods_len,
	};
	MonoMethod *register_jni_natives = registerType;
#endif // ndef NET || ndef ANDROID

#if !defined (NET)
	MonoDomain *domain = utils.get_current_domain (/* attach_thread_if_needed */ false);
	mono_jit_thread_attach (domain);
	// Refresh current domain as it might have been modified by the above call
	domain = mono_domain_get ();

	if constexpr (is_running_on_desktop) {
		MonoClass *runtime = utils.monodroid_get_class_from_name (domain, SharedConstants::MONO_ANDROID_ASSEMBLY_NAME, SharedConstants::ANDROID_RUNTIME_NS_NAME, SharedConstants::JNIENVINIT_CLASS_NAME);
		register_jni_natives = mono_class_get_method_from_name (runtime, "RegisterJniNatives", 5);
	}

	utils.monodroid_runtime_invoke (domain, register_jni_natives, nullptr, args, nullptr);
#else // ndef NET
	mono_jit_thread_attach (nullptr); // There's just one domain in .net

#if !defined (ANDROID)
	mono_runtime_invoke (register_jni_natives, nullptr, args, nullptr);
#else
	jnienv_register_jni_natives (managedType_ptr, managedType_len, nativeClass, methods_ptr, methods_len);
#endif // ndef ANDROID
#endif // def NET

	env->ReleaseStringChars (methods, methods_ptr);
	env->ReleaseStringChars (managedType, managedType_ptr);

	if (XA_UNLIKELY (FastTiming::enabled ())) {
		internal_timing->end_event (total_time_index, true /* uses_more_info */);

		dynamic_local_string<SENSIBLE_TYPE_NAME_LENGTH> type;
		const char *mt_ptr = env->GetStringUTFChars (managedType, nullptr);
		type.assign (mt_ptr, strlen (mt_ptr));
		env->ReleaseStringUTFChars (managedType, mt_ptr);

		internal_timing->add_more_info (total_time_index, type);
#if !defined (NET)
		dump_counters ("## Runtime.register: type=%s\n", type.get ());
#endif
	}
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_dumpTimingData ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jclass klass)
{
	if (internal_timing == nullptr) {
		return;
	}

	internal_timing->dump ();
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_register (JNIEnv *env, [[maybe_unused]] jclass klass, jstring managedType, jclass nativeClass, jstring methods)
{
	monodroidRuntime.Java_mono_android_Runtime_register (env, managedType, nativeClass, methods);
}

char*
MonodroidRuntime::get_java_class_name_for_TypeManager (jclass klass)
{
	if (klass == nullptr || Class_getName == nullptr)
		return nullptr;

	JNIEnv *env = osBridge.ensure_jnienv ();
	jstring name = reinterpret_cast<jstring> (env->CallObjectMethod (klass, Class_getName));
	if (name == nullptr) {
		log_error (LOG_DEFAULT, "Failed to obtain Java class name for object at %p", klass);
		return nullptr;
	}

	const char *mutf8 = env->GetStringUTFChars (name, nullptr);
	if (mutf8 == nullptr) {
		log_error (LOG_DEFAULT, "Failed to convert Java class name to UTF8 (out of memory?)");
		env->DeleteLocalRef (name);
		return nullptr;
	}
	char *ret = strdup (mutf8);

	env->ReleaseStringUTFChars (name, mutf8);
	env->DeleteLocalRef (name);

	char *dot = strchr (ret, '.');
	while (dot != nullptr) {
		*dot = '/';
		dot = strchr (dot + 1, '.');
	}

	return ret;
}

JNIEnv*
get_jnienv (void)
{
	return osBridge.ensure_jnienv ();
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_propagateUncaughtException (JNIEnv *env, [[maybe_unused]] jclass klass, jobject javaThread, jthrowable javaException)
{
#if defined (NET)
	monodroidRuntime.propagate_uncaught_exception (env, javaThread, javaException);
#else // def NET
	MonoDomain *domain = utils.get_current_domain ();
	monodroidRuntime.propagate_uncaught_exception (domain, env, javaThread, javaException);
#endif // ndef NET
}

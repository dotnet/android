#include <cstdio>
#include <cstdlib>
#include <cstring>

#include <host/os-bridge.hh>
#include <host/runtime-util.hh>
#include <runtime-base/logger.hh>
#include <shared/cpp-util.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

namespace {
	constexpr size_t LOG_LINE_BUFFER_SIZE = 512;

	void write_logcat_line (LogCategories category, const char *line, size_t line_len) noexcept
	{
		char local_buffer[LOG_LINE_BUFFER_SIZE];
		char *buffer = local_buffer;

		if (line_len >= sizeof (local_buffer)) {
			buffer = static_cast<char*>(malloc (line_len + 1));
			if (buffer == nullptr) {
				log_write (category, LogLevel::Debug, "<out of memory>");
				return;
			}
		}

		memcpy (buffer, line, line_len);
		buffer[line_len] = '\0';
		log_write (category, LogLevel::Debug, buffer);

		if (buffer != local_buffer) {
			free (buffer);
		}
	}
}

void OSBridge::initialize_on_onload (JavaVM *vm, JNIEnv *env) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");
	abort_if_invalid_pointer_argument (vm, "vm");

	jvm = vm;
	// jclass lref = env->FindClass ("java/lang/Runtime");
	// jmethodID Runtime_getRuntime = env->GetStaticMethodID (lref, "getRuntime", "()Ljava/lang/Runtime;");

	// Runtime_gc			= env->GetMethodID (lref, "gc", "()V");
	// Runtime_instance	= lref_to_gref (env, env->CallStaticObjectMethod (lref, Runtime_getRuntime));
	// env->DeleteLocalRef (lref);
	// lref = env->FindClass ("java/lang/ref/WeakReference");
	// weakrefClass = reinterpret_cast<jclass> (env->NewGlobalRef (lref));
	// env->DeleteLocalRef (lref);
	// weakrefCtor = env->GetMethodID (weakrefClass, "<init>", "(Ljava/lang/Object;)V");
	// weakrefGet = env->GetMethodID (weakrefClass, "get", "()Ljava/lang/Object;");

	// abort_unless (
	// 	weakrefClass != nullptr && weakrefCtor != nullptr && weakrefGet != nullptr,
	// 	"Failed to look up required java.lang.ref.WeakReference members"
	// );
}

void OSBridge::initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");
	GCUserPeer_class = RuntimeUtil::get_class_from_runtime_field(env, runtimeClass, "mono_android_GCUserPeer"sv, true);
	GCUserPeer_ctor	 = env->GetMethodID (GCUserPeer_class, "<init>", "()V");
	abort_unless (GCUserPeer_class != nullptr && GCUserPeer_ctor != nullptr, "Failed to load mono.android.GCUserPeer!");
}

auto OSBridge::lref_to_gref (JNIEnv *env, jobject lref) noexcept -> jobject
{
	if (lref == 0) {
		return 0;
	}

	jobject g = env->NewGlobalRef (lref);
	env->DeleteLocalRef (lref);
	return g;
}

auto OSBridge::get_object_ref_type (JNIEnv *env, void *handle) noexcept -> char
{
	jobjectRefType value;
	if (handle == nullptr)
		return 'I';
	value = env->GetObjectRefType (reinterpret_cast<jobject> (handle));
	switch (value) {
		case JNIInvalidRefType:     return 'I';
		case JNILocalRefType:       return 'L';
		case JNIGlobalRefType:      return 'G';
		case JNIWeakGlobalRefType:  return 'W';
		default:                    return '*';
	}
}

auto OSBridge::_monodroid_gref_inc () noexcept -> int
{
	return __sync_add_and_fetch (&gc_gref_count, 1);
}

auto OSBridge::_monodroid_gref_dec () noexcept -> int
{
	return __sync_sub_and_fetch (&gc_gref_count, 1);
}

auto OSBridge::_monodroid_weak_gref_inc () noexcept -> int
{
	return __sync_add_and_fetch (&gc_weak_gref_count, 1);
}

auto OSBridge::_monodroid_weak_gref_dec () noexcept -> int
{
	return __sync_sub_and_fetch (&gc_weak_gref_count, 1);
}

[[gnu::always_inline]]
void OSBridge::_write_stack_trace (FILE *to, const char *const from, LogCategories category) noexcept
{
	if (from == nullptr) [[unlikely]] {
		log_write (category, LogLevel::Warn, "Unable to write stack trace, managed runtime passed a NULL string.");
		return;
	}

	if (*from == '\0') [[unlikely]] {
		log_write (category, LogLevel::Warn, "Empty stack trace passed by the managed runtime.");
		return;
	}

	const char *line = from;
	while (line != nullptr && *line != '\0') {
		const char *newline = strchr (line, '\n');
		size_t line_len = newline == nullptr ? strlen (line) : static_cast<size_t>(newline - line);

		if ((category == LOG_GREF && Logger::gref_to_logcat ()) ||
			(category == LOG_LREF && Logger::lref_to_logcat ())) {
			write_logcat_line (category, line, line_len);
		}

		if (to != nullptr) {
			fwrite (line, sizeof (char), line_len, to);
			fputc ('\n', to);
			fflush (to);
		}

		if (newline == nullptr) {
			break;
		}
		line = newline + 1;
	}
}

void OSBridge::_monodroid_gref_log (const char *message) noexcept
{
	if (Logger::gref_to_logcat ()) {
		log_write (LOG_GREF, LogLevel::Debug, optional_string (message));
	}

	if (Logger::gref_log () == nullptr) {
		return;
	}

	fprintf (Logger::gref_log (), "%s", optional_string (message));
	fflush (Logger::gref_log ());
}

[[gnu::always_inline, gnu::flatten]]
void OSBridge::log_it (LogCategories category, const char *line, FILE *to, const char *const from, bool logcat_enabled) noexcept
{
	log_write (category, LogLevel::Info, line);

	// We skip logcat here when logging to file is enabled because _write_stack_trace will output to logcat as well, if enabled
	if (to == nullptr) {
		if (logcat_enabled) {
			_write_stack_trace (nullptr, from, category);
		}

		return;
	}

	fwrite (line, sizeof (char), strlen (line), to);
	fputc ('\n', to);

	_write_stack_trace (to, from, category);
	fflush (to);
}

auto OSBridge::_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from) noexcept -> int
{
	int c = _monodroid_gref_inc ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return c;
	}

	int wc = __atomic_load_n (&gc_weak_gref_count, __ATOMIC_RELAXED);
	char log_line[LOG_LINE_BUFFER_SIZE];
	snprintf (
		log_line,
		sizeof (log_line),
		"+g+ grefc %d gwrefc %d obj-handle %p/%c -> new-handle %p/%c from thread '%s'(%d)",
		c,
		wc,
		reinterpret_cast<void*>(curHandle),
		curType,
		reinterpret_cast<void*>(newHandle),
		newType,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_GREF, log_line, Logger::gref_log (), from, Logger::gref_to_logcat ());
	return c;
}

void OSBridge::_monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from) noexcept
{
	int c = _monodroid_gref_dec ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	int wc = __atomic_load_n (&gc_weak_gref_count, __ATOMIC_RELAXED);
	char log_line[LOG_LINE_BUFFER_SIZE];
	snprintf (
		log_line,
		sizeof (log_line),
		"-g- grefc %d gwrefc %d handle %p/%c from thread '%s'(%d)",
		c,
		wc,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_GREF, log_line, Logger::gref_log (), from, Logger::gref_to_logcat ());
}

void OSBridge::_monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from)
{
	int c = _monodroid_weak_gref_inc ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	int gc = __atomic_load_n (&gc_gref_count, __ATOMIC_RELAXED);
	char log_line[LOG_LINE_BUFFER_SIZE];
	snprintf (
		log_line,
		sizeof (log_line),
		"+w+ grefc %d gwrefc %d obj-handle %p/%c -> new-handle %p/%c from thread '%s'(%d)",
		gc,
		c,
		reinterpret_cast<void*>(curHandle),
		curType,
		reinterpret_cast<void*>(newHandle),
		newType,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_GREF, log_line, Logger::gref_log (), from, Logger::gref_to_logcat ());
}

void
OSBridge::_monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from)
{
	if ((log_categories & LOG_LREF) == 0) [[likely]] {
		return;
	}

	char log_line[LOG_LINE_BUFFER_SIZE];
	snprintf (
		log_line,
		sizeof (log_line),
		"+l+ lrefc %d handle %p/%c from thread '%s'(%d)",
		lrefc,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_LREF, log_line, Logger::lref_log (), from, Logger::lref_to_logcat ());
}

void OSBridge::_monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from)
{
	int c = _monodroid_weak_gref_dec ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	int gc = __atomic_load_n (&gc_gref_count, __ATOMIC_RELAXED);
	char log_line[LOG_LINE_BUFFER_SIZE];
	snprintf (
		log_line,
		sizeof (log_line),
		"-w- grefc %d gwrefc %d handle %p/%c from thread '%s'(%d)",
		gc,
		c,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_GREF, log_line, Logger::gref_log (), from, Logger::gref_to_logcat ());
}

void OSBridge::_monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from)
{
	if ((log_categories & LOG_LREF) == 0) [[likely]] {
		return;
	}

	char log_line[LOG_LINE_BUFFER_SIZE];
	snprintf (
		log_line,
		sizeof (log_line),
		"-l- lrefc %d handle %p/%c from thread '%s'(%d)",
		lrefc,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_LREF, log_line, Logger::lref_log (), from, Logger::lref_to_logcat ());
}

#include <cstdarg>
#include <cstdlib>

#include <host/os-bridge.hh>
#include <host/runtime-util.hh>
#include <runtime-base/logger.hh>
#include <shared/cpp-util.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

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
		log_warn (category, "Unable to write stack trace, managed runtime passed a NULL string.");
		return;
	}

	std::string_view trace { from };
	if (trace.empty ()) [[unlikely]] {
		log_warn (category, "Empty stack trace passed by the managed runtime.");
		return;
	}

	while (true) {
		size_t line_end = trace.find ('\n');
		size_t line_length = line_end == std::string_view::npos ? trace.length () : line_end;
		std::string_view line { trace.data (), line_length };

		if ((category == LOG_GREF && Logger::gref_to_logcat ()) ||
			(category == LOG_LREF && Logger::lref_to_logcat ())) {
				log_debugf (category, "%.*s", static_cast<int>(line.length ()), line.data ());
		}

		if (to != nullptr) {
			fwrite (line.data (), sizeof (std::string_view::value_type), line.length (), to);
			fputc ('\n', to);
			fflush (to);
		}

		if (line_end == std::string_view::npos) {
			break;
		}

		trace.remove_prefix (line_end + 1);
	}
}

void OSBridge::_monodroid_gref_log (const char *message) noexcept
{
	if (Logger::gref_to_logcat ()) {
		log_debugf (LOG_GREF, "%s", optional_string (message));
	}

	if (Logger::gref_log () == nullptr) {
		return;
	}

	fprintf (Logger::gref_log (), "%s", optional_string (message));
	fflush (Logger::gref_log ());
}

void OSBridge::gref_logf (const char *format, ...) noexcept
{
	const char *safe_format = format == nullptr ? "<null>" : format;
	va_list args;
	va_start (args, format);

	if (Logger::gref_to_logcat () && (log_categories & LOG_GREF) != 0) {
		va_list logcat_args;
		va_copy (logcat_args, args);
		log_writev (LOG_GREF, LogLevel::Debug, safe_format, logcat_args);
		va_end (logcat_args);
	}

	FILE *gref_log = Logger::gref_log ();
	if (gref_log != nullptr) {
		vfprintf (gref_log, safe_format, args);
		fflush (gref_log);
	}

	va_end (args);
}

[[gnu::always_inline, gnu::flatten]]
void OSBridge::log_it (LogCategories category, std::string_view const& line, FILE *to, const char *const from, bool logcat_enabled) noexcept
{
	log_write (category, LogLevel::Info, line);

	// We skip logcat here when logging to file is enabled because _write_stack_trace will output to logcat as well, if enabled
	if (to == nullptr) {
		if (logcat_enabled) {
			_write_stack_trace (nullptr, from, category);
		}

		return;
	}

	fwrite (line.data (), sizeof (std::string_view::value_type), line.length (), to);
	fputc ('\n', to);

	_write_stack_trace (to, from, category);
	fflush (to);
}

void OSBridge::log_itf (LogCategories category, FILE *to, const char *const from, bool logcat_enabled, const char *format, ...) noexcept
{
	const char *safe_format = format == nullptr ? "<null>" : format;
	char *line = nullptr;
	va_list args;
	va_start (args, format);
	int length = vasprintf (&line, safe_format, args);
	va_end (args);

	if (length < 0) [[unlikely]] {
		log_it (category, safe_format, to, from, logcat_enabled);
		return;
	}

	log_it (category, std::string_view { line, static_cast<size_t>(length) }, to, from, logcat_enabled);
	std::free (line);
}

auto OSBridge::_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from) noexcept -> int
{
	int c = _monodroid_gref_inc ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return c;
	}

	int wc = __atomic_load_n (&gc_weak_gref_count, __ATOMIC_RELAXED);
	log_itf (
		LOG_GREF,
		Logger::gref_log (),
		from,
		Logger::gref_to_logcat (),
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
	return c;
}

void OSBridge::_monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from) noexcept
{
	int c = _monodroid_gref_dec ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	int wc = __atomic_load_n (&gc_weak_gref_count, __ATOMIC_RELAXED);
	log_itf (
		LOG_GREF,
		Logger::gref_log (),
		from,
		Logger::gref_to_logcat (),
		"-g- grefc %d gwrefc %d handle %p/%c from thread '%s'(%d)",
		c,
		wc,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);
}

void OSBridge::_monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from)
{
	int c = _monodroid_weak_gref_inc ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	int gc = __atomic_load_n (&gc_gref_count, __ATOMIC_RELAXED);
	log_itf (
		LOG_GREF,
		Logger::gref_log (),
		from,
		Logger::gref_to_logcat (),
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
}

void
OSBridge::_monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from)
{
	if ((log_categories & LOG_LREF) == 0) [[likely]] {
		return;
	}

	log_itf (
		LOG_LREF,
		Logger::lref_log (),
		from,
		Logger::lref_to_logcat (),
		"+l+ lrefc %d handle %p/%c from thread '%s'(%d)",
		lrefc,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);
}

void OSBridge::_monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from)
{
	int c = _monodroid_weak_gref_dec ();
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	int gc = __atomic_load_n (&gc_gref_count, __ATOMIC_RELAXED);
	log_itf (
		LOG_GREF,
		Logger::gref_log (),
		from,
		Logger::gref_to_logcat (),
		"-w- grefc %d gwrefc %d handle %p/%c from thread '%s'(%d)",
		gc,
		c,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);
}

void OSBridge::_monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from)
{
	if ((log_categories & LOG_LREF) == 0) [[likely]] {
		return;
	}

	log_itf (
		LOG_LREF,
		Logger::lref_log (),
		from,
		Logger::lref_to_logcat (),
		"-l- lrefc %d handle %p/%c from thread '%s'(%d)",
		lrefc,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);
}

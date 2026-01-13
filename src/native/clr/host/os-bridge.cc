#include <ranges>

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

[[gnu::always_inline]]
void OSBridge::_write_stack_trace (FILE *to, const char *const from, LogCategories category) noexcept
{
	if (from == nullptr) [[unlikely]] {
		log_warn (category, "Unable to write stack trace, managed runtime passed a NULL string.");
		return;
	}

	const std::string_view trace { from };
	if (trace.empty ()) [[unlikely]] {
		log_warn (category, "Empty stack trace passed by the managed runtime.");
		return;
	}

	for (const auto segment : std::views::split (trace, '\n')) {
		const std::string_view line { segment };

		if ((category == LOG_GREF && Logger::gref_to_logcat ()) ||
			(category == LOG_LREF && Logger::lref_to_logcat ())) {
				log_debug (
					category,
#if defined(XA_HOST_NATIVEAOT)
					"%s",
					line.data ()
#else
					"{}"sv,
					line
#endif
				);
		}

		if (to == nullptr) {
			continue;
		}

		fwrite (line.data (), sizeof (std::string_view::value_type), line.length (), to);
		fputc ('\n', to);
		fflush (to);
	}
}

void OSBridge::_monodroid_gref_log (const char *message) noexcept
{
	if (Logger::gref_to_logcat ()) {
		log_debug (
			LOG_GREF,
#if defined(XA_HOST_NATIVEAOT)
			"%s",
#else
			"{}"sv,
#endif
			optional_string (message)
		);
	}

	if (Logger::gref_log () == nullptr) {
		return;
	}

	fprintf (Logger::gref_log (), "%s", optional_string (message));
	fflush (Logger::gref_log ());
}

[[gnu::always_inline, gnu::flatten]]
void OSBridge::log_it (LogCategories category, std::string const& line, FILE *to, const char *const from, bool logcat_enabled) noexcept
{
	log_write (category, LogLevel::Info, line);

	// We skip logcat here when logging to file is enabled because _write_stack_trace will output to logcat as well, if enabled
	if (to == nullptr) {
		if (logcat_enabled) {
			_write_stack_trace (nullptr, from, category);
		}

		return;
	}

	fwrite (line.c_str (), sizeof (std::string::value_type), line.length (), to);
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

	const std::string log_line = std::format (
		"+g+ grefc {} gwrefc {} obj-handle {:p}/{} -> new-handle {:p}/{} from thread '{}'({})"sv,
		c,
		gc_weak_gref_count,
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

	const std::string log_line = std::format (
		"-g- grefc {} gwrefc {} handle {:p}/{} from thread '{}'({})"sv,
		c,
		gc_weak_gref_count,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_GREF, log_line, Logger::gref_log (), from, Logger::gref_to_logcat ());
}

void OSBridge::_monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from)
{
	++gc_weak_gref_count;
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	const std::string log_line = std::format (
		"+w+ grefc {} gwrefc {} obj-handle {:p}/{} -> new-handle {:p}/{} from thread '{}'({})"sv,
		gc_gref_count,
		gc_weak_gref_count,
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

	const std::string log_line = std::format (
		"+l+ lrefc {} handle {:p}/{} from thread '{}'({})"sv,
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
	--gc_weak_gref_count;
	if ((log_categories & LOG_GREF) == 0) [[likely]] {
		return;
	}

	const std::string log_line = std::format (
		"-w- grefc {} gwrefc {} handle {:p}/{} from thread '{}'({})"sv,
		gc_gref_count,
		gc_weak_gref_count,
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

	const std::string log_line = std::format (
		"-l- lrefc {} handle {:p}/{} from thread '{}'({})"sv,
		lrefc,
		reinterpret_cast<void*>(handle),
		type,
		optional_string (threadName),
		threadId
	);

	log_it (LOG_LREF, log_line, Logger::lref_log (), from, Logger::lref_to_logcat ());
}

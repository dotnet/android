#include <host/host-util.hh>
#include <host/os-bridge.hh>
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
	GCUserPeer_class = HostUtil::get_class_from_runtime_field(env, runtimeClass, "mono_android_GCUserPeer", true);
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

auto OSBridge::_monodroid_gref_inc () noexcept -> int
{
	return __sync_add_and_fetch (&gc_gref_count, 1);
}

auto OSBridge::_monodroid_gref_dec () noexcept -> int
{
	return __sync_sub_and_fetch (&gc_gref_count, 1);
}

[[gnu::always_inline]]
auto OSBridge::_get_stack_trace_line_end (char *m) noexcept -> char*
{
	while (*m && *m != '\n') {
		m++;
	}

	return m;
}

[[gnu::always_inline]]
void OSBridge::_write_stack_trace (FILE *to, char *from, LogCategories category) noexcept
{
	char *n = const_cast<char*> (from);

	char c;
	do {
		char *m		= n;
		char *end	= _get_stack_trace_line_end (m);

		n		= end + 1;
		c		= *end;
		*end	= '\0';
		if ((category == LOG_GREF && Logger::gref_to_logcat ()) ||
			(category == LOG_LREF && Logger::lref_to_logcat ())) {
				log_debug (category, "{}", optional_string (m));
		}

		if (to != nullptr) {
			fprintf (to, "%s\n", optional_string (m));
			fflush (to);
		}
		*end	= c;
	} while (c);
}

void OSBridge::_monodroid_gref_log (const char *message) noexcept
{
	if (Logger::gref_to_logcat ()) {
		log_debug (LOG_GREF, "{}", optional_string (message));
	}

	if (Logger::gref_log () == nullptr) {
		return;
	}

	fprintf (Logger::gref_log (), "%s", optional_string (message));
	fflush (Logger::gref_log ());
}

auto OSBridge::_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable) noexcept -> int
{
	int c = _monodroid_gref_inc ();
	if ((log_categories & LOG_GREF) == 0) {
		return c;
	}

	log_info (LOG_GREF,
			  "+g+ grefc {} gwrefc {} obj-handle {:p}/{} -> new-handle {:p}/{} from thread '{}'({})",
			  c,
			  gc_weak_gref_count,
			  reinterpret_cast<void*>(curHandle),
			  curType,
			  reinterpret_cast<void*>(newHandle),
			  newType,
			  optional_string (threadName),
			  threadId
	);

	if (Logger::gref_to_logcat ()) {
		if (from_writable) {
			_write_stack_trace (nullptr, const_cast<char*>(from), LOG_GREF);
		} else {
			log_info (LOG_GREF, "{}", optional_string (from));
		}
	}

	if (Logger::gref_log () == nullptr) {
		return c;
	}

	fprintf (
		Logger::gref_log (),
		"+g+ grefc %i gwrefc %i obj-handle %p/%c -> new-handle %p/%c from thread '%s'(%i)\n",
		c,
		gc_weak_gref_count,
		curHandle,
		curType,
		newHandle,
		newType,
		optional_string (threadName),
		threadId
	);

	if (from_writable) {
		_write_stack_trace (Logger::gref_log (), const_cast<char*>(from));
	} else {
		fprintf (Logger::gref_log (), "%s\n", from);
	}

	fflush (Logger::gref_log ());
	return c;
}

void OSBridge::_monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable) noexcept
{
	int c = _monodroid_gref_dec ();
	if ((log_categories & LOG_GREF) == 0) {
		return;
	}

	log_info (LOG_GREF,
			  "-g- grefc {} gwrefc {} handle {:p}/{} from thread '{}'({})",
			  c,
			  gc_weak_gref_count,
			  reinterpret_cast<void*>(handle),
			  type,
			  optional_string (threadName),
			  threadId
	);
	if (Logger::gref_to_logcat ()) {
		if (from_writable) {
			_write_stack_trace (nullptr, const_cast<char*>(from), LOG_GREF);
		} else {
			log_info (LOG_GREF, "{}", optional_string (from));
		}
	}

	if (Logger::gref_log () == nullptr) {
		return;
	}

	fprintf (Logger::gref_log (),
			 "-g- grefc %i gwrefc %i handle %p/%c from thread '%s'(%i)\n",
			 c,
			 gc_weak_gref_count,
			 handle,
			 type,
			 optional_string (threadName),
			 threadId
	);

	if (from_writable) {
		_write_stack_trace (Logger::gref_log (), const_cast<char*>(from));
	} else {
		fprintf (Logger::gref_log(), "%s\n", optional_string (from));
	}

	fflush (Logger::gref_log ());
}

#include <cstdarg>
#include <cstdlib>

#include <host/os-bridge.hh>
#include <host/runtime-util.hh>
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

void OSBridge::set_reference_logging_callbacks (reference_log_fn log_callback, reference_log_message_fn message_callback) noexcept
{
	abort_if_invalid_pointer_argument (log_callback, "log_callback");
	abort_if_invalid_pointer_argument (message_callback, "message_callback");
	reference_log_callback = log_callback;
	reference_log_message_callback = message_callback;
}

void OSBridge::log_reference (
	ReferenceLogEvent kind,
	jobject current_handle,
	char current_type,
	jobject new_handle,
	char new_type,
	const char *thread_name,
	int thread_id,
	const char *stack_trace) noexcept
{
	abort_if_invalid_pointer_argument (reference_log_callback, "reference_log_callback");
	reference_log_callback (
		kind,
		current_handle,
		static_cast<uint8_t>(current_type),
		new_handle,
		static_cast<uint8_t>(new_type),
		thread_name,
		thread_id,
		stack_trace);
}

void OSBridge::log_reference_message (const char *message) noexcept
{
	if (!Logger::gref_enabled ()) [[likely]] {
		return;
	}

	abort_if_invalid_pointer_argument (reference_log_message_callback, "reference_log_message_callback");
	reference_log_message_callback (message);
}

void OSBridge::log_reference_messagef (const char *format, ...) noexcept
{
	if (!Logger::gref_enabled ()) [[likely]] {
		return;
	}

	const char *safe_format = optional_string (format);
	char *message = nullptr;
	va_list args;
	va_start (args, format);
	int length = vasprintf (&message, safe_format, args);
	va_end (args);

	if (length < 0) [[unlikely]] {
		log_reference_message (safe_format);
		return;
	}

	log_reference_message (message);
	std::free (message);
}

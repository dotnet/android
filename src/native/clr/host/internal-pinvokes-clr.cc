#include <host/gc-bridge.hh>
#include <host/host.hh>
#include <host/os-bridge.hh>
#include <host/typemap.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/internal-pinvokes.hh>
#include <runtime-base/jni-remapping.hh>

using namespace xamarin::android;

const char* clr_typemap_managed_to_java (const char *typeName, const uint8_t *mvid) noexcept
{
	return TypeMapper::managed_to_java (typeName, mvid);
}

bool clr_typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept
{
	return TypeMapper::java_to_managed (java_type_name, assembly_name, managed_type_token_id);
}

const char*
_monodroid_lookup_replacement_type (const char *jniSimpleReference)
{
	return JniRemapping::lookup_replacement_type (jniSimpleReference);
}

const JniRemappingReplacementMethod*
_monodroid_lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature)
{
	return JniRemapping::lookup_replacement_method_info (jniSourceType, jniMethodName, jniMethodSignature);
}

managed_timing_sequence* monodroid_timing_start (const char *message)
{
	// Technically a reference here is against the idea of shared pointers, but
	// in this instance it's fine since we know we won't be storing the pointer
	// and this way things are slightly faster.
	std::shared_ptr<Timing> const &timing = Host::get_timing ();
	if (!timing) {
		return nullptr;
	}

	managed_timing_sequence *ret = timing->get_available_sequence ();
	if (message != nullptr) {
		log_write (LOG_TIMING, LogLevel::Info, message);
	}
	ret->start = FastTiming::get_time ();
	return ret;
}

void monodroid_timing_stop (managed_timing_sequence *sequence, const char *message)
{
	constexpr std::string_view DEFAULT_MESSAGE { "Managed Timing" };
	if (sequence == nullptr) {
		return;
	}

	std::shared_ptr<Timing> const &timing = Host::get_timing ();
	if (!timing) [[unlikely]] {
		return;
	}

	sequence->end = FastTiming::get_time ();
	Timing::info (sequence, message == nullptr ? DEFAULT_MESSAGE.data () : message);
	timing->release_sequence (sequence);
}

void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	OSBridge::_monodroid_weak_gref_delete (handle, type, threadName, threadId, from, from_writable);
}

void* _monodroid_timezone_get_default_id ()
{
	JNIEnv *env			 = OSBridge::ensure_jnienv ();
	jmethodID getDefault = env->GetStaticMethodID (Host::get_java_class_TimeZone (), "getDefault", "()Ljava/util/TimeZone;");
	jmethodID getID		 = env->GetMethodID (Host::get_java_class_TimeZone (), "getID",		 "()Ljava/lang/String;");
	jobject d			 = env->CallStaticObjectMethod (Host::get_java_class_TimeZone (), getDefault);
	jstring id			 = reinterpret_cast<jstring> (env->CallObjectMethod (d, getID));
	const char *mutf8	 = env->GetStringUTFChars (id, nullptr);
	if (mutf8 == nullptr) {
		log_error (LOG_DEFAULT, "Failed to convert Java TimeZone ID to UTF8 (out of memory?)"sv);
		env->DeleteLocalRef (id);
		env->DeleteLocalRef (d);
		return nullptr;
	}
	char *def_id		 = strdup (mutf8);
	env->ReleaseStringUTFChars (id, mutf8);
	env->DeleteLocalRef (id);
	env->DeleteLocalRef (d);
	return def_id;
}

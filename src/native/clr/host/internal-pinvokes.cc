#include <host/host.hh>
#include <host/os-bridge.hh>
#include <host/typemap.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/internal-pinvokes.hh>
#include <runtime-base/jni-remapping.hh>

using namespace xamarin::android;

int _monodroid_gref_get () noexcept
{
	return OSBridge::get_gc_gref_count ();
}

void _monodroid_gref_log (const char *message) noexcept
{
	OSBridge::_monodroid_gref_log (message);
}

int _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable) noexcept
{
	return OSBridge::_monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}

void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable) noexcept
{
	// NOTE: disabled until GC bridge is in
	// It currently causes a crash in Mono.Android_Tests:
	//
	//  FATAL EXCEPTION: Instr: xamarin.android.runtimetests.NUnitInstrumentation
	//  Process: Mono.Android.NET_Tests, PID: 16194
	//  android.runtime.JavaProxyThrowable: [System.ObjectDisposedException]: Cannot access disposed object with JniIdentityHashCode=166175665.
	//  Object name: 'Xamarin.Android.RuntimeTests.NUnitInstrumentation'.
	//         at Java.Interop.JniPeerMembers.AssertSelf + 0x2f(Unknown Source)
	//         at Java.Interop.JniPeerMembers+JniInstanceMethods.InvokeVirtualVoidMethod + 0x1(Unknown Source)
	//         at Android.App.Instrumentation.Finish + 0x46(Unknown Source)
	//         at Xamarin.Android.UnitTests.TestInstrumentation`1.OnStart + 0x6d(Unknown Source)
	//         at Android.App.Instrumentation.n_OnStart + 0x1f(Unknown Source)
	//         at crc643df67da7b13bb6b1.TestInstrumentation_1.n_onStart(Native Method)
	//         at crc643df67da7b13bb6b1.TestInstrumentation_1.onStart(TestInstrumentation_1.java:32)
	//         at android.app.Instrumentation$InstrumentationThread.run(Instrumentation.java:2606)
	// OSBridge::_monodroid_gref_log_delete (handle, type, threadName, threadId, from, from_writable);
}

const char* clr_typemap_managed_to_java (const char *typeName, const uint8_t *mvid) noexcept
{
	return TypeMapper::managed_to_java (typeName, mvid);
}

bool clr_typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept
{
	return TypeMapper::java_to_managed (java_type_name, assembly_name, managed_type_token_id);
}

void monodroid_log (LogLevel level, LogCategories category, const char *message) noexcept
{
	switch (level) {
		case LogLevel::Verbose:
		case LogLevel::Debug:
			log_debug_nocheck (category, std::string_view { message });
			break;

		case LogLevel::Info:
			log_info_nocheck (category, std::string_view { message });
			break;

		case LogLevel::Warn:
		case LogLevel::Silent: // warn is always printed
			log_warn (category, std::string_view { message });
			break;

		case LogLevel::Error:
			log_error (category, std::string_view { message });
			break;

		case LogLevel::Fatal:
			log_fatal (category, std::string_view { message });
			break;

		default:
		case LogLevel::Unknown:
		case LogLevel::Default:
			log_info_nocheck (category, std::string_view { message });
			break;
	}
}

char* monodroid_TypeManager_get_java_class_name (jclass klass) noexcept
{
	return Host::get_java_class_name_for_TypeManager (klass);
}

void monodroid_free (void *ptr) noexcept
{
	free (ptr);
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

void _monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
	OSBridge::_monodroid_weak_gref_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}

int _monodroid_weak_gref_get ()
{
	return OSBridge::get_gc_weak_gref_count ();
}

int _monodroid_max_gref_get ()
{
	return static_cast<int>(AndroidSystem::get_max_gref_count ());
}

void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	OSBridge::_monodroid_weak_gref_delete (handle, type, threadName, threadId, from, from_writable);
}

void _monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	OSBridge::_monodroid_lref_log_new (lrefc, handle, type, threadName, threadId, from, from_writable);
}

void _monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	OSBridge::_monodroid_lref_log_delete (lrefc, handle, type, threadName, threadId, from, from_writable);
}

void _monodroid_gc_wait_for_bridge_processing ()
{
	// mono_gc_wait_for_bridge_processing (); - replace with the new GC bridge call, when we have it
}

void _monodroid_detect_cpu_and_architecture (uint16_t *built_for_cpu, uint16_t *running_on_cpu, unsigned char *is64bit)
{
	abort_if_invalid_pointer_argument (built_for_cpu, "built_for_cpu");
	abort_if_invalid_pointer_argument (running_on_cpu, "running_on_cpu");
	abort_if_invalid_pointer_argument (is64bit, "is64bit");

	bool _64bit;
	monodroid_detect_cpu_and_architecture (*built_for_cpu, *running_on_cpu, _64bit);
	*is64bit = _64bit;
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

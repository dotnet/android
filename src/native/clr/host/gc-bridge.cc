#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

void GCBridge::initialize_on_load (JNIEnv *env) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");

	jclass lref = env->FindClass ("java/lang/Runtime");
	jmethodID Runtime_getRuntime = env->GetStaticMethodID (lref, "getRuntime", "()Ljava/lang/Runtime;");
	Runtime_gc = env->GetMethodID (lref, "gc", "()V");
	Runtime_instance = OSBridge::lref_to_gref (env, env->CallStaticObjectMethod (lref, Runtime_getRuntime));
	env->DeleteLocalRef (lref);

	abort_unless (
		Runtime_gc != nullptr && Runtime_instance != nullptr,
		"Failed to look up Java GC runtime API."
	);
}

[[gnu::always_inline]]
void GCBridge::trigger_java_gc () noexcept
{
	JNIEnv *env = OSBridge::ensure_jnienv ();

	// NOTE: Mono has a number of pre- and post- calls before invoking the Java GC. At this point
	// it is unknown whether the CoreCLR GC bridge will need anything of that sort, so we just trigger
	// the Java GC here.
	env->CallVoidMethod (Runtime_instance, Runtime_gc);
}

void GCBridge::mark_cross_references (size_t sccsLen, StronglyConnectedComponent* sccs, size_t ccrsLen,	ComponentCrossReference* ccrs) noexcept
{
	if (bridge_processing_finish_callback == nullptr) [[unlikely]] {
		return;
	}

	trigger_java_gc ();

	// Call back into managed code
	bridge_processing_finish_callback (sccsLen, sccs, ccrsLen, ccrs);
}

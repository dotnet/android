#include <host/host-util.hh>
#include <host/os-bridge.hh>
#include <shared/cpp-util.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

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

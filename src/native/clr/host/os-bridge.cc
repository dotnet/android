#include <host/os-bridge.hh>

using namespace xamarin::android;

void OSBridge::initialize_on_onload (JavaVM *vm) noexcept
{
	abort_if_invalid_pointer_argument (vm, "vm");

	jvm = vm;
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

#include <pthread.h>

#include <host/os-bridge.hh>

using namespace xamarin::android;

namespace {
	pthread_key_t jni_env_key;
	pthread_once_t jni_env_key_once = PTHREAD_ONCE_INIT;
}

void OSBridge::create_jni_env_key () noexcept
{
	int ret = pthread_key_create (&jni_env_key, detach_current_thread);
	abort_unless (ret == 0, "Unable to create TLS key for JNI thread detachment");
}

void OSBridge::register_thread_detach (JNIEnv *env) noexcept
{
	int ret = pthread_once (&jni_env_key_once, create_jni_env_key);
	abort_unless (ret == 0, "Unable to initialize TLS key for JNI thread detachment");
	ret = pthread_setspecific (jni_env_key, env);
	abort_unless (ret == 0, "Unable to register JNI thread detachment callback");
}

void OSBridge::detach_current_thread ([[maybe_unused]] void *value) noexcept
{
	if (jvm == nullptr) [[unlikely]] {
		return;
	}

	JNIEnv *env = nullptr;
	if (jvm->GetEnv (reinterpret_cast<void**> (&env), JNI_VERSION_1_6) == JNI_OK && env != nullptr) {
		jvm->DetachCurrentThread ();
	}
}

void OSBridge::initialize_on_onload (JavaVM *vm) noexcept
{
	abort_if_invalid_pointer_argument (vm, "vm");

	jvm = vm;
	int ret = pthread_once (&jni_env_key_once, create_jni_env_key);
	abort_unless (ret == 0, "Unable to initialize TLS key for JNI thread detachment");
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

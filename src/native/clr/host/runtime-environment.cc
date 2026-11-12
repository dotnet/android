#include <pthread.h>

#include <runtime-base/runtime-environment.hh>
#include <shared/cpp-util.hh>

using namespace xamarin::android;

namespace {
	pthread_key_t jni_env_key;
	pthread_once_t jni_env_key_once = PTHREAD_ONCE_INIT;
}

void RuntimeEnvironment::create_jni_env_key () noexcept
{
	int ret = pthread_key_create (&jni_env_key, detach_current_thread);
	abort_unless (ret == 0, "Unable to create TLS key for JNI thread detachment");
}

void RuntimeEnvironment::register_thread_detach (JNIEnv *env) noexcept
{
	int ret = pthread_once (&jni_env_key_once, create_jni_env_key);
	abort_unless (ret == 0, "Unable to initialize TLS key for JNI thread detachment");
	ret = pthread_setspecific (jni_env_key, env);
	abort_unless (ret == 0, "Unable to register JNI thread detachment callback");
}

void RuntimeEnvironment::detach_current_thread ([[maybe_unused]] void *value) noexcept
{
	if (jvm == nullptr) [[unlikely]] {
		return;
	}

	JNIEnv *env = nullptr;
	if (jvm->GetEnv (reinterpret_cast<void**> (&env), JNI_VERSION_1_6) == JNI_OK && env != nullptr) {
		jvm->DetachCurrentThread ();
	}
}

void RuntimeEnvironment::initialize (JavaVM *vm) noexcept
{
	abort_if_invalid_pointer_argument (vm, "vm");
	jvm = vm;
	int ret = pthread_once (&jni_env_key_once, create_jni_env_key);
	abort_unless (ret == 0, "Unable to initialize TLS key for JNI thread detachment");
}

auto RuntimeEnvironment::get_jnienv () noexcept -> JNIEnv*
{
	JNIEnv *env = nullptr;
	jvm->GetEnv (reinterpret_cast<void**> (&env), JNI_VERSION_1_6);
	if (env == nullptr) {
		JavaVMAttachArgs args;
		args.version = JNI_VERSION_1_6;
		args.name = nullptr;
		args.group = nullptr;
		jint status = jvm->AttachCurrentThread (&env, &args);
		abort_unless (status == JNI_OK && env != nullptr, "Unable to get a valid pointer to JNIEnv");
		register_thread_detach (env);
	}

	return env;
}

#include <climits>
#include <cstdint>
#include <pthread.h>

#include <runtime-base/runtime-environment.hh>
#include <shared/cpp-util.hh>

using namespace xamarin::android;

namespace {
	pthread_key_t jni_env_key;
	pthread_once_t jni_env_key_once = PTHREAD_ONCE_INIT;

	// jni_env_key holds a destructor round number (1 to PTHREAD_DESTRUCTOR_ITERATIONS), not a pointer.
	// libc only needs the value to be non-null.
	auto round_value (uintptr_t round) noexcept -> void*
	{
		return reinterpret_cast<void*> (round);
	}

	auto round_of (void *value) noexcept -> uintptr_t
	{
		return reinterpret_cast<uintptr_t> (value);
	}
}

void RuntimeEnvironment::create_jni_env_key () noexcept
{
	int ret = pthread_key_create (&jni_env_key, detach_current_thread);
	abort_unless (ret == 0, "Unable to create TLS key for JNI thread detachment");
}

void RuntimeEnvironment::register_thread_detach ([[maybe_unused]] JNIEnv *env) noexcept
{
	int ret = pthread_once (&jni_env_key_once, create_jni_env_key);
	abort_unless (ret == 0, "Unable to initialize TLS key for JNI thread detachment");
	// Cleanup is already registered: keep its round. Another destructor may detach and re-attach the thread
	// while it exits; restarting at round 1 would run out of rounds before the detach and leak the attachment.
	if (pthread_getspecific (jni_env_key) != nullptr) {
		return;
	}
	ret = pthread_setspecific (jni_env_key, round_value (1));
	abort_unless (ret == 0, "Unable to register JNI thread detachment callback");
}

// Detaches a thread that this runtime attached to the JavaVM, when that thread exits.
//
// JNI requires a thread attached with AttachCurrentThread to call DetachCurrentThread before it
// exits, and ART does not do it for us. On API 24+, an attached thread that exits stays registered
// with the VM. That leaks its java.lang.Thread, native memory and one of ART's internal thread IDs;
// once the IDs run out (after about 65,000 such threads), ART aborts the process with
// "Out of internal thread ids". Android's JNI guidance is to detach from a pthread key destructor,
// because the destructor runs on the exiting thread itself.
//
// We wait until the last destructor round before detaching. When a thread exits, bionic first runs
// C++ thread_local destructors and pthread cleanup handlers. It then runs key destructors in up to
// PTHREAD_DESTRUCTOR_ITERATIONS (4) rounds. In each round it walks the keys in index order and, for
// every key that has a non-null value on this thread, clears the value and calls the destructor with
// the old value. It starts another round only if a destructor stored a new non-null value.
//
// jni_env_key is created in JNI_OnLoad, so it usually has a lower index than the keys created by
// native libraries that are loaded later. If we detached in the first round, we would pull the
// attachment out from under those libraries' destructors. Examples:
//   * a destructor that sends per-thread data to Java after GetEnv, which would now get
//     JNI_EDETACHED and silently drop the data;
//   * a destructor that uses a JNIEnv* it saved earlier, which would be using a detached thread's
//     environment;
//   * a library that detached our attachment, attached the thread again itself, and detaches its own
//     attachment from its destructor; we would detach it first and break its cleanup.
//
// Instead, until the last round, we store a non-null value in jni_env_key again without detaching.
// That makes libc call us again in the next round. Ordinary destructors run once, in the first round,
// so by the last round they have all run while the thread was still attached. Only then do we detach
// whatever attachment is still current. If another component has already detached the thread,
// GetEnv returns JNI_EDETACHED and there is nothing left to do.
//
// This is best effort. Nothing reports which destructors are still pending or whether they need JNI.
// A destructor for a key with a higher index than ours that also re-arms itself until the last round
// still runs after us, and finds the thread detached.
//
// Before API 24, ART used its own pthread key to catch threads that exit while attached. It aborted
// the process if the thread was still attached in its second round, before our last round. .NET for
// Android requires API 24 or newer, where ART no longer does this.
void RuntimeEnvironment::detach_current_thread (void *value) noexcept
{
	uintptr_t round = round_of (value);
	// If storing the next round fails, libc won't call us again, so fall through and detach now.
	if (round < PTHREAD_DESTRUCTOR_ITERATIONS && pthread_setspecific (jni_env_key, round_value (round + 1)) == 0) {
		return;
	}

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

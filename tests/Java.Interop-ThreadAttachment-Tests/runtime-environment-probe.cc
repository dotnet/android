#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <pthread.h>

#include <runtime-base/runtime-environment.hh>
#include <shared/helpers.hh>

using xamarin::android::RuntimeEnvironment;

namespace xamarin::android {
	[[noreturn]] void Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
	{
		std::fprintf (stderr, "%s\n", message);
		std::abort ();
	}
}

namespace {
	std::atomic<int> attaches { 0 };
	std::atomic<int> detaches { 0 };
	std::atomic<int> errors { 0 };
	// Only one fake-JVM worker runs at a time; its state must outlive C++ thread-local storage.
	std::atomic<bool> attached { false };
	JNIEnv environment {};

	enum class AttachmentCase {
		NativeOwned,
		ManagedOwned,
		PreattachedAndDetached,
		DetachedManually,
		Preattached,
	};

	jint attach (JavaVM*, JNIEnv **env, void*)
	{
		if (!attached) {
			attached = true;
			++attaches;
		}
		*env = &environment;
		return JNI_OK;
	}

	jint detach (JavaVM*)
	{
		if (!attached) {
			++errors;
			return JNI_EDETACHED;
		}
		attached = false;
		++detaches;
		return JNI_OK;
	}

	jint get_env (JavaVM*, void **env, jint)
	{
		*env = attached ? &environment : nullptr;
		return attached ? JNI_OK : JNI_EDETACHED;
	}

	void *worker (void *argument)
	{
		auto mode = *static_cast<AttachmentCase*> (argument);
		JNIEnv *env = nullptr;
		JavaVM *vm = RuntimeEnvironment::get_java_vm ();

		switch (mode) {
			case AttachmentCase::NativeOwned:
				env = RuntimeEnvironment::get_jnienv ();
				break;
			case AttachmentCase::ManagedOwned:
				vm->AttachCurrentThread (&env, nullptr);
				RuntimeEnvironment::register_thread_detach (env);
				break;
			case AttachmentCase::PreattachedAndDetached:
				vm->AttachCurrentThread (&env, nullptr);
				env = RuntimeEnvironment::get_jnienv ();
				vm->DetachCurrentThread ();
				break;
			case AttachmentCase::DetachedManually:
				env = RuntimeEnvironment::get_jnienv ();
				vm->DetachCurrentThread ();
				break;
			case AttachmentCase::Preattached:
				vm->AttachCurrentThread (&env, nullptr);
				env = RuntimeEnvironment::get_jnienv ();
				break;
		}
		if (env != &environment)
			++errors;
		return nullptr;
	}

	void check (bool condition, const char *message)
	{
		if (!condition) {
			std::fprintf (stderr, "%s\n", message);
			std::abort ();
		}
	}
}

int main ()
{
	JNIInvokeInterface table {};
	table.AttachCurrentThread = &attach;
	table.DetachCurrentThread = &detach;
	table.GetEnv = &get_env;
	JavaVM vm { &table };
	RuntimeEnvironment::initialize (&vm);

	AttachmentCase cases [] = {
		AttachmentCase::NativeOwned,
		AttachmentCase::ManagedOwned,
		AttachmentCase::PreattachedAndDetached,
		AttachmentCase::DetachedManually,
		AttachmentCase::Preattached,
	};
	for (auto mode : cases) {
		int previous_attaches = attaches;
		int previous_detaches = detaches;
		pthread_t thread;
		check (pthread_create (&thread, nullptr, worker, &mode) == 0, "pthread_create failed");
		check (pthread_join (thread, nullptr) == 0, "pthread_join failed");
		check (attaches == previous_attaches + 1, "Wrong attach count");
		int expected_detaches = mode == AttachmentCase::Preattached ? 0 : 1;
		check (detaches == previous_detaches + expected_detaches, "Wrong detach count");
		check (errors == 0, "Unexpected JNI state");
	}

	std::puts ("Passed five pthread attachment and detachment cases.");
}

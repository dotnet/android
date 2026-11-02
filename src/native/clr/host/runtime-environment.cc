#include <runtime-base/runtime-environment.hh>
#include <shared/cpp-util.hh>

using namespace xamarin::android;

void RuntimeEnvironment::initialize (JavaVM *vm) noexcept
{
	abort_if_invalid_pointer_argument (vm, "vm");
	jvm = vm;
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
		jvm->AttachCurrentThread (&env, &args);
		abort_unless (env != nullptr, "Unable to get a valid pointer to JNIEnv");
	}

	return env;
}

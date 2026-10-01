#include <cstdio>
#include <cstdlib>
#include <source_location>
#include <string>

#include <runtime-base/app-system-properties.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

// Keep the production reader's failures observable without aborting the test runner's JVM host.
void Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	std::fprintf (stderr, "%s\n", message);
	std::exit (73);
}

extern "C" JNIEXPORT jint JNICALL JNI_OnLoad (JavaVM *vm, void*)
{
	JNIEnv *env = nullptr;
	if (vm->GetEnv (reinterpret_cast<void**>(&env), JNI_VERSION_1_6) != JNI_OK || env == nullptr) {
		return JNI_ERR;
	}
	AppSystemProperties::initialize (env);
	return JNI_VERSION_1_6;
}

extern "C" JNIEXPORT void JNICALL Java_BootstrapProbe_reinitialize (JNIEnv *env, jclass)
{
	AppSystemProperties::initialize (env);
}

extern "C" JNIEXPORT jbyteArray JNICALL Java_BootstrapProbe_lookup (JNIEnv *env, jclass, jbyteArray name)
{
	jsize length = env->GetArrayLength (name);
	std::string key (static_cast<size_t>(length), '\0');
	env->GetByteArrayRegion (name, 0, length, reinterpret_cast<jbyte*>(key.data ()));
	if (env->ExceptionCheck ()) {
		return nullptr;
	}
	size_t value_length = 0;
	const char *value = AppSystemProperties::lookup (key.c_str (), value_length);
	if (value == nullptr) {
		if (value_length != 0) {
			Helpers::abort_application ("Missing property must reset its length");
		}
		return nullptr;
	}
	jbyteArray result = env->NewByteArray (static_cast<jsize>(value_length));
	if (result != nullptr) {
		env->SetByteArrayRegion (result, 0, static_cast<jsize>(value_length), reinterpret_cast<const jbyte*>(value));
	}
	return result;
}

#include <cstdio>
#include <cstdlib>
#include <source_location>
#include <string>
#include <jni.h>

#include <runtime-base/app-system-properties.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

// Keep the production reader's failures observable without aborting the test runner's JVM host.
void Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	std::fprintf (stderr, "%s\n", message);
	std::exit (EXIT_FAILURE);
}

extern "C" JNIEXPORT void JNICALL Java_net_dot_jni_nativeaot_BootstrapProbe_initialize (JNIEnv *env, jclass, jbyteArray body)
{
	jsize length = env->GetArrayLength (body);
	auto data = static_cast<uint8_t*>(std::malloc (static_cast<size_t>(length)));
	if (data == nullptr) {
		Helpers::abort_application ("Unable to allocate test bootstrap body");
	}
	env->GetByteArrayRegion (body, 0, length, reinterpret_cast<jbyte*>(data));
	AppSystemProperties::initialize ({ data, static_cast<uint32_t>(length) });
}

extern "C" JNIEXPORT jbyteArray JNICALL Java_net_dot_jni_nativeaot_BootstrapProbe_lookup (JNIEnv *env, jclass, jbyteArray name)
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

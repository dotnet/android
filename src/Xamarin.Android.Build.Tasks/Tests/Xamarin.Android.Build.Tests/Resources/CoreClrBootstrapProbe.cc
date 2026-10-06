#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <source_location>

#include <runtime-base/java-app-config.hh>
#include <shared/log_types.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

void Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	std::fprintf (stderr, "%s\n", message);
	std::exit (EXIT_FAILURE);
}

void Helpers::abort_applicationf (LogCategories, std::source_location, const char *format, ...) noexcept
{
	va_list args;
	va_start (args, format);
	std::vfprintf (stderr, format, args);
	va_end (args);
	std::exit (EXIT_FAILURE);
}

extern "C" JNIEXPORT jint JNICALL JNI_OnLoad (JavaVM *vm, void*)
{
	JNIEnv *env = nullptr;
	if (vm->GetEnv (reinterpret_cast<void**>(&env), JNI_VERSION_1_6) != JNI_OK || env == nullptr) {
		return JNI_ERR;
	}
	JavaAppConfig::initialize (env);
	return JNI_VERSION_1_6;
}

extern "C" JNIEXPORT jbyteArray JNICALL Java_net_dot_android_CoreClrBootstrapProbe_snapshot (JNIEnv *env, jclass, jboolean jni_flags)
{
	const char *blob = JavaAppConfig::package_name ();
	const char *next = blob;
	auto check_pointer = [&next] (const char *value) {
		if (value != next) {
			Helpers::abort_application ("All configuration pointers must reference the same owned blob");
		}
		next += std::strlen (value) + 1;
	};
	check_pointer (JavaAppConfig::package_name ());
	for (auto const& entry : JavaAppConfig::environment ()) {
		check_pointer (entry.name);
		check_pointer (entry.value);
	}
	for (auto const& entry : JavaAppConfig::system_properties ()) {
		check_pointer (entry.name);
		check_pointer (entry.value);
		size_t length = 0;
		if (JavaAppConfig::lookup_system_property (entry.name, length) != entry.value || length != std::strlen (entry.value)) {
			Helpers::abort_application ("System property lookup must use the blob");
		}
	}
	const char *reserved[] { "HOST_RUNTIME_CONTRACT", "RUNTIME_IDENTIFIER", "APP_CONTEXT_BASE_DIRECTORY" };
	for (size_t i = 0; i < 3; i++) {
		if (std::strcmp (JavaAppConfig::runtime_property_names ()[i], reserved[i]) != 0 ||
			JavaAppConfig::runtime_property_values ()[i] != nullptr) {
			Helpers::abort_application ("Reserved hosting properties must retain their startup slots");
		}
	}
	for (int i = 3; i < JavaAppConfig::runtime_property_count (); i++) {
		check_pointer (JavaAppConfig::runtime_property_names ()[i]);
		check_pointer (JavaAppConfig::runtime_property_values ()[i]);
	}
	size_t index = 0;
	for (auto const& library : JavaAppConfig::libraries ()) {
		check_pointer (library.name);
		bool expected_jni = jni_flags == JNI_TRUE;
		bool expected_preload = expected_jni && index == 0;
		if (library.is_jni_library != expected_jni || library.preload != expected_preload || library.handle != nullptr) {
			Helpers::abort_application ("Native library flags and initial handles must be preserved");
		}
		index++;
	}
	jsize length = static_cast<jsize>(next - blob);
	jbyteArray bytes = env->NewByteArray (length);
	if (bytes != nullptr) {
		env->SetByteArrayRegion (bytes, 0, length, reinterpret_cast<const jbyte*>(blob));
	}
	return bytes;
}

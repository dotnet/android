#include <climits>
#include <cstdint>
#include <cstdlib>
#include <source_location>

#include <runtime-base/app-bootstrap-properties.hh>
#include <runtime-base/java-app-config.hh>
#include <runtime-base/logger.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

namespace {
	struct Strings
	{
		char **values;
		size_t count;
	};

	template<typename T>
	auto allocate_items (size_t count, const char *description) noexcept -> T*
	{
		if (count == 0) {
			return nullptr;
		}

		size_t size = Helpers::multiply_with_overflow_check<size_t> (count, sizeof (T));
		auto items = static_cast<T*>(std::calloc (1, size));
		if (items == nullptr) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Unable to allocate %s", description);
		}
		return items;
	}

	auto require_field (JNIEnv *env, jclass config_class, const char *name, const char *signature) noexcept -> jfieldID
	{
		jfieldID field = env->GetStaticFieldID (config_class, name, signature);
		if (field == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Unable to read Java application config field '%s'", name);
		}
		return field;
	}

	auto read_string (JNIEnv *env, jclass config_class, const char *field_name) noexcept -> char*
	{
		jfieldID field = require_field (env, config_class, field_name, "Ljava/lang/String;");
		auto value = static_cast<jstring>(env->GetStaticObjectField (config_class, field));
		if (value == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid Java application config field '%s'", field_name);
		}
		char *result = AppBootstrapProperties::copy_java_string (env, value);
		env->DeleteLocalRef (value);
		return result;
	}

	auto read_strings (JNIEnv *env, jclass config_class, const char *field_name, size_t stride) noexcept -> Strings
	{
		jfieldID field = require_field (env, config_class, field_name, "[Ljava/lang/String;");
		auto array = static_cast<jobjectArray>(env->GetStaticObjectField (config_class, field));
		if (array == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid Java application config array '%s'", field_name);
		}

		jsize length = env->GetArrayLength (array);
		if (length < 0 || static_cast<size_t>(length) % stride != 0) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid length of Java application config array '%s'", field_name);
		}

		auto values = allocate_items<char*> (static_cast<size_t>(length), field_name);
		for (jsize i = 0; i < length; i++) {
			auto element = static_cast<jstring>(env->GetObjectArrayElement (array, i));
			if (element == nullptr || env->ExceptionCheck ()) {
				Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid string in Java application config array '%s'", field_name);
			}
			values[i] = AppBootstrapProperties::copy_java_string (env, element);
			env->DeleteLocalRef (element);
		}
		env->DeleteLocalRef (array);
		return { values, static_cast<size_t>(length) };
	}

	auto read_boolean (JNIEnv *env, jclass config_class, const char *field_name) noexcept -> bool
	{
		jfieldID field = require_field (env, config_class, field_name, "Z");
		return env->GetStaticBooleanField (config_class, field) == JNI_TRUE;
	}

	auto read_integer (JNIEnv *env, jclass config_class, const char *field_name) noexcept -> uint32_t
	{
		jfieldID field = require_field (env, config_class, field_name, "I");
		return static_cast<uint32_t>(env->GetStaticIntField (config_class, field));
	}

}

void JavaAppConfig::initialize (JNIEnv *env) noexcept
{
	if (enabled) {
		Helpers::abort_application (LOG_DEFAULT, "Java application configuration has already been initialized");
	}

	jclass config_class = env->FindClass ("net/dot/android/AppBootstrapConfig");
	if (config_class == nullptr || env->ExceptionCheck ()) {
		Helpers::abort_application (LOG_DEFAULT, "Unable to load AppBootstrapConfig");
	}

	AppBootstrapProperties::initialize (env);
	config.android_package_name = read_string (env, config_class, "PackageName");
	config.package_naming_policy = read_integer (env, config_class, "PackageNamingPolicy");
	config.have_assembly_store = read_boolean (env, config_class, "HaveAssemblyStore");

	Strings runtime_values = read_strings (env, config_class, "RuntimeProperties", 2);
	size_t runtime_count = Helpers::add_with_overflow_check<size_t> (runtime_values.count / 2, 3uz);
	if (runtime_count > INT_MAX) {
		Helpers::abort_application (LOG_DEFAULT, "Too many CoreCLR runtime properties");
	}
	property_names = allocate_items<const char*> (runtime_count, "runtime property names");
	property_values = allocate_items<char*> (runtime_count, "runtime property values");
	property_names[0] = "HOST_RUNTIME_CONTRACT";
	property_names[1] = "RUNTIME_IDENTIFIER";
	property_names[2] = "APP_CONTEXT_BASE_DIRECTORY";
	for (size_t i = 3; i < runtime_count; i++) {
		property_names[i] = runtime_values.values[(i - 3) * 2];
		property_values[i] = runtime_values.values[(i - 3) * 2 + 1];
	}
	std::free (runtime_values.values);
	config.number_of_runtime_properties = static_cast<uint32_t>(runtime_count);

	Strings library_names = read_strings (env, config_class, "NativeLibraries", 1);
	native_library_count = library_names.count;
	native_libraries = allocate_items<Library> (native_library_count, "native libraries");

	jfieldID flags_field = require_field (env, config_class, "NativeLibraryFlags", "[B");
	auto flags_array = static_cast<jbyteArray>(env->GetStaticObjectField (config_class, flags_field));
	if (flags_array == nullptr || env->ExceptionCheck () || static_cast<size_t>(env->GetArrayLength (flags_array)) != native_library_count) {
		Helpers::abort_application (LOG_DEFAULT, "Invalid Java native library flags");
	}
	jbyte *flags = env->GetByteArrayElements (flags_array, nullptr);
	if (flags == nullptr) {
		Helpers::abort_application (LOG_DEFAULT, "Unable to read Java native library flags");
	}
	for (size_t i = 0; i < native_library_count; i++) {
		auto value = static_cast<uint8_t>(flags[i]);
		if ((value & ~0x03u) != 0 || ((value & 2u) != 0 && (value & 1u) == 0)) {
			Helpers::abort_application (LOG_DEFAULT, "Invalid Java native library preload flags");
		}
		native_libraries[i] = { library_names.values[i], (value & 1u) != 0, (value & 2u) != 0, nullptr };
	}
	env->ReleaseByteArrayElements (flags_array, flags, JNI_ABORT);
	env->DeleteLocalRef (flags_array);
	std::free (library_names.values);

	env->DeleteLocalRef (config_class);
	enabled = true;
}

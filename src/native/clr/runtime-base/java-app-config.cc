#include <climits>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <source_location>

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

	auto require_field (JNIEnv *env, jclass config, const char *name, const char *signature) noexcept -> jfieldID
	{
		jfieldID field = env->GetStaticFieldID (config, name, signature);
		if (field == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Unable to read Java application config field '%s'", name);
		}
		return field;
	}

	auto copy_string (JNIEnv *env, jstring value) noexcept -> char*
	{
		jsize length = env->GetStringLength (value);
		const jchar *characters = env->GetStringChars (value, nullptr);
		if (length < 0 || characters == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_application (LOG_DEFAULT, "Unable to read application bootstrap string");
		}
		size_t capacity = Helpers::add_with_overflow_check<size_t> (
			Helpers::multiply_with_overflow_check<size_t> (static_cast<size_t>(length), 3uz), 1uz);
		char *result = allocate_items<char> (capacity, "application bootstrap string");
		size_t position = 0;
		for (jsize i = 0; i < length; i++) {
			uint32_t codepoint = characters[i];
			if (codepoint >= 0xd800 && codepoint <= 0xdbff) {
				if (i + 1 < length && characters[i + 1] >= 0xdc00 && characters[i + 1] <= 0xdfff) {
					codepoint = 0x10000 + ((codepoint - 0xd800) << 10) + (characters[++i] - 0xdc00);
				} else {
					codepoint = 0xfffd;
				}
			} else if (codepoint >= 0xdc00 && codepoint <= 0xdfff) {
				codepoint = 0xfffd;
			}
			if (codepoint == 0) {
				Helpers::abort_application (LOG_DEFAULT, "NUL character in application bootstrap string");
			}
			if (codepoint <= 0x7f) {
				result[position++] = static_cast<char>(codepoint);
			} else if (codepoint <= 0x7ff) {
				result[position++] = static_cast<char>(0xc0 | (codepoint >> 6));
				result[position++] = static_cast<char>(0x80 | (codepoint & 0x3f));
			} else if (codepoint <= 0xffff) {
				result[position++] = static_cast<char>(0xe0 | (codepoint >> 12));
				result[position++] = static_cast<char>(0x80 | ((codepoint >> 6) & 0x3f));
				result[position++] = static_cast<char>(0x80 | (codepoint & 0x3f));
			} else {
				result[position++] = static_cast<char>(0xf0 | (codepoint >> 18));
				result[position++] = static_cast<char>(0x80 | ((codepoint >> 12) & 0x3f));
				result[position++] = static_cast<char>(0x80 | ((codepoint >> 6) & 0x3f));
				result[position++] = static_cast<char>(0x80 | (codepoint & 0x3f));
			}
		}
		result[position] = '\0';
		env->ReleaseStringChars (value, characters);
		return result;
	}

	auto read_string (JNIEnv *env, jclass config, const char *name) noexcept -> char*
	{
		jfieldID field = require_field (env, config, name, "Ljava/lang/String;");
		auto value = static_cast<jstring>(env->GetStaticObjectField (config, field));
		if (value == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid Java application config field '%s'", name);
		}
		char *result = copy_string (env, value);
		env->DeleteLocalRef (value);
		return result;
	}

	auto read_strings (JNIEnv *env, jclass config, const char *name, size_t stride) noexcept -> Strings
	{
		jfieldID field = require_field (env, config, name, "[Ljava/lang/String;");
		auto array = static_cast<jobjectArray>(env->GetStaticObjectField (config, field));
		if (array == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid Java application config array '%s'", name);
		}
		jsize length = env->GetArrayLength (array);
		if (length < 0 || static_cast<size_t>(length) % stride != 0) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid length of Java application config array '%s'", name);
		}
		char **values = allocate_items<char*> (static_cast<size_t>(length), name);
		for (jsize i = 0; i < length; i++) {
			auto element = static_cast<jstring>(env->GetObjectArrayElement (array, i));
			if (element == nullptr || env->ExceptionCheck ()) {
				Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid string in Java application config array '%s'", name);
			}
			values[i] = copy_string (env, element);
			env->DeleteLocalRef (element);
		}
		env->DeleteLocalRef (array);
		return { values, static_cast<size_t>(length) };
	}

	auto read_pairs (JNIEnv *env, jclass config, const char *name, size_t &count) noexcept -> JavaAppConfig::Entry*
	{
		Strings strings = read_strings (env, config, name, 2);
		count = strings.count / 2;
		auto entries = allocate_items<JavaAppConfig::Entry> (count, name);
		for (size_t i = 0; i < count; i++) {
			entries[i] = { strings.values[i * 2], strings.values[i * 2 + 1] };
		}
		std::free (strings.values);
		return entries;
	}
}

void JavaAppConfig::initialize (JNIEnv *env) noexcept
{
	if (initialized) {
		Helpers::abort_application (LOG_DEFAULT, "Java application configuration has already been initialized");
	}
	jclass config = env->FindClass ("net/dot/android/AppBootstrapConfig");
	if (config == nullptr || env->ExceptionCheck ()) {
		Helpers::abort_application (LOG_DEFAULT, "Unable to load AppBootstrapConfig");
	}

	android_package_name = read_string (env, config, "PackageName");
	naming_policy = static_cast<uint32_t>(env->GetStaticIntField (config, require_field (env, config, "PackageNamingPolicy", "I")));
	assembly_store_enabled = env->GetStaticBooleanField (config, require_field (env, config, "HaveAssemblyStore", "Z")) == JNI_TRUE;
	split_configs_ignored = env->GetStaticBooleanField (config, require_field (env, config, "IgnoreSplitConfigs", "Z")) == JNI_TRUE;
	environment_entries = read_pairs (env, config, "Environment", environment_count);
	system_property_entries = read_pairs (env, config, "SystemProperties", system_property_count);

	Strings runtime = read_strings (env, config, "RuntimeProperties", 2);
	size_t count = Helpers::add_with_overflow_check<size_t> (runtime.count / 2, 3uz);
	if (count > INT_MAX) {
		Helpers::abort_application (LOG_DEFAULT, "Too many CoreCLR runtime properties");
	}
	property_count = static_cast<int>(count);
	property_names = allocate_items<const char*> (count, "runtime property names");
	property_values = allocate_items<char*> (count, "runtime property values");
	property_names[0] = "HOST_RUNTIME_CONTRACT";
	property_names[1] = "RUNTIME_IDENTIFIER";
	property_names[2] = "APP_CONTEXT_BASE_DIRECTORY";
	for (size_t i = 3; i < count; i++) {
		property_names[i] = runtime.values[(i - 3) * 2];
		property_values[i] = runtime.values[(i - 3) * 2 + 1];
	}
	std::free (runtime.values);

	Strings names = read_strings (env, config, "NativeLibraries", 1);
	native_library_count = names.count;
	native_libraries = allocate_items<Library> (native_library_count, "native libraries");
	jfieldID flags_field = require_field (env, config, "NativeLibraryFlags", "[B");
	auto flags_array = static_cast<jbyteArray>(env->GetStaticObjectField (config, flags_field));
	if (flags_array == nullptr || env->ExceptionCheck () ||
		static_cast<size_t>(env->GetArrayLength (flags_array)) != native_library_count) {
		Helpers::abort_application (LOG_DEFAULT, "Invalid Java native library flags");
	}
	if (native_library_count > 0) {
		jbyte *flags = env->GetByteArrayElements (flags_array, nullptr);
		if (flags == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_application (LOG_DEFAULT, "Unable to read Java native library flags");
		}
		for (size_t i = 0; i < native_library_count; i++) {
			auto value = static_cast<uint8_t>(flags[i]);
			if ((value & ~0x03u) != 0 || ((value & 2u) != 0 && (value & 1u) == 0)) {
				Helpers::abort_application (LOG_DEFAULT, "Invalid Java native library preload flags");
			}
			native_libraries[i] = { names.values[i], (value & 1u) != 0, (value & 2u) != 0, nullptr };
		}
		env->ReleaseByteArrayElements (flags_array, flags, JNI_ABORT);
	}
	env->DeleteLocalRef (flags_array);
	std::free (names.values);
	env->DeleteLocalRef (config);
	initialized = true;
}

auto JavaAppConfig::lookup_system_property (const char *name, size_t &value_length) noexcept -> const char*
{
	value_length = 0;
	for (Entry const& entry : system_properties ()) {
		if (std::strcmp (name, entry.name) == 0) {
			value_length = std::strlen (entry.value);
			return entry.value;
		}
	}
	return nullptr;
}

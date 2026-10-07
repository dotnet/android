#include <climits>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <source_location>
#include <type_traits>

#include <runtime-base/java-app-config.hh>
#include <shared/log_types.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

namespace {
	template<typename T>
	auto allocate_items (size_t count, const char *description, bool zero_initialize = true) noexcept -> T*
	{
		if (count == 0) {
			return nullptr;
		}
		size_t size = Helpers::multiply_with_overflow_check<size_t> (count, sizeof (T));
		auto items = static_cast<T*>(zero_initialize ? std::calloc (1, size) : std::malloc (size));
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

	template<typename T>
	auto read_array (JNIEnv *env, jclass config, const char *name) noexcept -> std::span<T>
	{
		static_assert (std::is_same_v<T, jbyte> || std::is_same_v<T, jint>);
		jfieldID field = require_field (env, config, name, std::is_same_v<T, jbyte> ? "[B" : "[I");
		jobject array = env->GetStaticObjectField (config, field);
		if (array == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid Java application config array '%s'", name);
		}
		jsize length = env->GetArrayLength (static_cast<jarray>(array));
		if (length < 0 || env->ExceptionCheck ()) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid length of Java application config array '%s'", name);
		}
		T *values = allocate_items<T> (static_cast<size_t>(length), name, false);
		if (length > 0) {
			if constexpr (std::is_same_v<T, jbyte>) {
				env->GetByteArrayRegion (static_cast<jbyteArray>(array), 0, length, values);
			} else {
				env->GetIntArrayRegion (static_cast<jintArray>(array), 0, length, values);
			}
			if (env->ExceptionCheck ()) {
				Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Unable to read Java application config array '%s'", name);
			}
		}
		env->DeleteLocalRef (array);
		return { values, static_cast<size_t>(length) };
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

	auto data = read_array<jbyte> (env, config, "NativeConfig");
	auto layout = read_array<jint> (env, config, "NativeConfigLayout");
	auto flags = read_array<jbyte> (env, config, "NativeLibraryFlags");
	if (layout.size () < 5 || data.empty ()) {
		Helpers::abort_application (LOG_DEFAULT, "Invalid Java application bootstrap layout");
	}
	size_t string_count = 1;
	for (size_t i = 0; i < 4; i++) {
		if (layout[i] < 0) {
			Helpers::abort_application (LOG_DEFAULT, "Invalid Java application bootstrap count");
		}
		size_t count = Helpers::multiply_with_overflow_check<size_t> (static_cast<size_t>(layout[i]), i == 3 ? 1uz : 2uz);
		string_count = Helpers::add_with_overflow_check<size_t> (string_count, count);
	}
	auto offsets = layout.subspan (4);
	if (offsets.size () != string_count || offsets[0] != 0 || flags.size () != static_cast<size_t>(layout[3])) {
		Helpers::abort_application (LOG_DEFAULT, "Invalid Java application bootstrap offsets");
	}
	for (size_t i = 0; i < offsets.size (); i++) {
		size_t end = i + 1 < offsets.size () ? static_cast<size_t>(offsets[i + 1]) : data.size ();
		if (offsets[i] < 0 || end > data.size () || static_cast<size_t>(offsets[i]) >= end || data[end - 1] != 0) {
			Helpers::abort_application (LOG_DEFAULT, "Invalid Java application bootstrap string bounds");
		}
	}
	bootstrap_data = reinterpret_cast<char*>(data.data ());
	auto string_at = [&offsets] (size_t index) noexcept -> char* {
		return bootstrap_data + offsets[index];
	};
	size_t index = 0;
	android_package_name = string_at (index++);
	naming_policy = static_cast<uint32_t>(env->GetStaticIntField (config, require_field (env, config, "PackageNamingPolicy", "I")));
	assembly_store_enabled = env->GetStaticBooleanField (config, require_field (env, config, "HaveAssemblyStore", "Z")) == JNI_TRUE;
	split_configs_ignored = env->GetStaticBooleanField (config, require_field (env, config, "IgnoreSplitConfigs", "Z")) == JNI_TRUE;
	if (env->ExceptionCheck ()) {
		Helpers::abort_application (LOG_DEFAULT, "Unable to read Java application package settings");
	}

	auto read_pairs = [&index, &string_at] (size_t count, const char *name) noexcept -> Entry* {
		auto entries = allocate_items<Entry> (count, name);
		for (size_t i = 0; i < count; i++) {
			entries[i] = { string_at (index++), string_at (index++) };
		}
		return entries;
	};
	environment_count = static_cast<size_t>(layout[0]);
	environment_entries = read_pairs (environment_count, "environment variables");
	system_property_count = static_cast<size_t>(layout[1]);
	system_property_entries = read_pairs (system_property_count, "system properties");

	size_t count = Helpers::add_with_overflow_check<size_t> (static_cast<size_t>(layout[2]), 3uz);
	if (count > INT_MAX) {
		Helpers::abort_application (LOG_DEFAULT, "Too many CoreCLR runtime properties");
	}
	property_count = static_cast<int>(count);
	property_names = allocate_items<const char*> (count, "runtime property names");
	property_values = allocate_items<const char*> (count, "runtime property values");
	property_names[0] = "HOST_RUNTIME_CONTRACT";
	property_names[1] = "RUNTIME_IDENTIFIER";
	property_names[2] = "APP_CONTEXT_BASE_DIRECTORY";
	for (size_t i = 3; i < count; i++) {
		property_names[i] = string_at (index++);
		property_values[i] = string_at (index++);
	}

	native_library_count = static_cast<size_t>(layout[3]);
	native_libraries = allocate_items<Library> (native_library_count, "native libraries");
	for (size_t i = 0; i < native_library_count; i++) {
		auto value = static_cast<uint8_t>(flags[i]);
		if ((value & ~0x03u) != 0 || ((value & 2u) != 0 && (value & 1u) == 0)) {
			Helpers::abort_application (LOG_DEFAULT, "Invalid Java native library preload flags");
		}
		native_libraries[i] = { string_at (index++), (value & 1u) != 0, (value & 2u) != 0, nullptr };
	}
	std::free (flags.data ());
	std::free (layout.data ());
	env->DeleteLocalRef (config);
	initialized = true;
}

auto JavaAppConfig::runtime_property_values (const char *host_contract, const char *runtime_identifier, const char *base_directory) noexcept -> const char**
{
	property_values[0] = host_contract;
	property_values[1] = runtime_identifier;
	property_values[2] = base_directory;
	return property_values;
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

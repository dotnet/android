#include <cstdint>
#include <cstdlib>
#include <cstring>

#include <runtime-base/app-system-properties.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

namespace {
	void ensure_jni_success (JNIEnv *env, bool valid, const char *message) noexcept
	{
		if (!valid || env->ExceptionCheck ()) {
			if (env->ExceptionCheck ()) {
				env->ExceptionDescribe ();
			}
			Helpers::abort_application (message);
		}
	}
}

auto AppSystemProperties::copy_java_string (JNIEnv *env, jstring value) noexcept -> char*
{
	ensure_jni_success (env, value != nullptr, "Null NativeAOT system property string");
	jsize length = env->GetStringLength (value);
	ensure_jni_success (env, length >= 0, "Unable to read NativeAOT system property string length");
	const jchar *characters = env->GetStringChars (value, nullptr);
	ensure_jni_success (env, characters != nullptr, "Unable to read NativeAOT system property string");

	size_t capacity = Helpers::add_with_overflow_check<size_t> (
		Helpers::multiply_with_overflow_check<size_t> (static_cast<size_t>(length), 3uz), 1uz
	);
	char *result = static_cast<char*>(std::calloc (capacity, 1));
	if (result == nullptr) {
		Helpers::abort_application ("Unable to allocate NativeAOT system property string");
	}

	// JNI's GetStringUTFChars returns modified UTF-8, not the UTF-8 used by native property callers.
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
			Helpers::abort_application ("NUL character in NativeAOT system property string");
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
	ensure_jni_success (env, true, "Unable to release NativeAOT system property string");
	return result;
}

void AppSystemProperties::initialize (JNIEnv *env) noexcept
{
	if (env == nullptr) {
		Helpers::abort_application ("JNI environment is required to read NativeAOT system properties");
	}
	if (initialized) {
		return;
	}

	jclass config = env->FindClass ("net/dot/jni/nativeaot/NativeAotEnvironmentVars");
	ensure_jni_success (env, config != nullptr, "Unable to load NativeAotEnvironmentVars");
	jfieldID field = env->GetStaticFieldID (config, "systemProperties", "[Ljava/lang/String;");
	ensure_jni_success (env, field != nullptr, "Unable to find NativeAOT system properties");
	auto properties = static_cast<jobjectArray>(env->GetStaticObjectField (config, field));
	ensure_jni_success (env, properties != nullptr, "Null NativeAOT system property array");
	jsize length = env->GetArrayLength (properties);
	ensure_jni_success (env, length >= 0 && (length & 1) == 0, "Invalid NativeAOT system property count");

	property_count = static_cast<size_t>(length) / 2;
	if (property_count != 0) {
		size_t capacity = Helpers::multiply_with_overflow_check<size_t> (property_count, sizeof (Entry));
		property_entries = static_cast<Entry*>(std::calloc (capacity, 1));
		if (property_entries == nullptr) {
			Helpers::abort_application ("Unable to allocate NativeAOT system properties");
		}
	}
	for (size_t i = 0; i < property_count; i++) {
		auto name = static_cast<jstring>(env->GetObjectArrayElement (properties, static_cast<jsize>(i * 2)));
		ensure_jni_success (env, name != nullptr, "Null NativeAOT system property name");
		auto value = static_cast<jstring>(env->GetObjectArrayElement (properties, static_cast<jsize>(i * 2 + 1)));
		ensure_jni_success (env, value != nullptr, "Null NativeAOT system property value");
		property_entries[i] = { copy_java_string (env, name), copy_java_string (env, value) };
		if (property_entries[i].name[0] == '\0') {
			Helpers::abort_application ("Empty NativeAOT system property name");
		}
		env->DeleteLocalRef (name);
		env->DeleteLocalRef (value);
	}
	env->DeleteLocalRef (properties);
	env->DeleteLocalRef (config);
	initialized = true;
}

auto AppSystemProperties::lookup (const char *name, size_t &value_length) noexcept -> const char*
{
	if (!initialized || name == nullptr) {
		Helpers::abort_application ("NativeAOT system properties must be initialized before lookup");
	}
	value_length = 0;
	for (Entry const& property : entries ()) {
		if (std::strcmp (name, property.name) == 0) {
			value_length = std::strlen (property.value);
			return property.value;
		}
	}
	return nullptr;
}

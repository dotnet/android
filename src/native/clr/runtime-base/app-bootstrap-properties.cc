#include <cstdint>
#include <cstdlib>
#include <cstring>

#include <constants.hh>
#include <runtime-base/app-bootstrap-properties.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

auto AppBootstrapProperties::copy_java_string (JNIEnv *env, jstring value) noexcept -> char*
{
	if (value == nullptr) {
		Helpers::abort_application ("Null string in application bootstrap configuration");
	}

	jsize length = env->GetStringLength (value);
	const jchar *characters = env->GetStringChars (value, nullptr);
	if (length < 0 || characters == nullptr) {
		Helpers::abort_application ("Unable to read application bootstrap string");
	}

	size_t capacity = Helpers::add_with_overflow_check<size_t> (
		Helpers::multiply_with_overflow_check<size_t> (static_cast<size_t>(length), 3uz), 1uz
	);
	char *result = static_cast<char*>(std::calloc (capacity, 1));
	if (result == nullptr) {
		Helpers::abort_application ("Unable to allocate application bootstrap string");
	}

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
			Helpers::abort_application ("NUL character in application bootstrap string");
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

void AppBootstrapProperties::initialize (JNIEnv *env) noexcept
{
	if (initialized) {
		return;
	}

	jclass config = env->FindClass ("net/dot/android/AppBootstrapConfig");
	if (config == nullptr || env->ExceptionCheck ()) {
		Helpers::abort_application ("Unable to load AppBootstrapConfig");
	}
	jfieldID field = env->GetStaticFieldID (config, "SystemProperties", "[Ljava/lang/String;");
	if (field == nullptr || env->ExceptionCheck ()) {
		Helpers::abort_application ("Unable to read application system properties");
	}
	auto values = static_cast<jobjectArray>(env->GetStaticObjectField (config, field));
	if (values == nullptr || env->ExceptionCheck ()) {
		Helpers::abort_application ("Invalid application system properties");
	}
	jsize length = env->GetArrayLength (values);
	if (length < 0 || (length & 1) != 0) {
		Helpers::abort_application ("Invalid application system property count");
	}

	property_count = static_cast<size_t>(length) / 2;
	if (property_count != 0) {
		property_entries = static_cast<Entry*>(
			std::calloc (property_count, sizeof (Entry))
		);
		if (property_entries == nullptr) {
			Helpers::abort_application ("Unable to allocate application system properties");
		}
	}
	for (size_t i = 0; i < property_count; i++) {
		auto name = static_cast<jstring>(env->GetObjectArrayElement (values, static_cast<jsize>(i * 2)));
		auto value = static_cast<jstring>(env->GetObjectArrayElement (values, static_cast<jsize>(i * 2 + 1)));
		if (name == nullptr || value == nullptr || env->ExceptionCheck ()) {
			Helpers::abort_application ("Invalid application system property entry");
		}
		property_entries[i] = { copy_java_string (env, name), copy_java_string (env, value) };
		env->DeleteLocalRef (name);
		env->DeleteLocalRef (value);
	}
	env->DeleteLocalRef (values);

	jmethodID read_remapping = env->GetStaticMethodID (config, "readRemappingAsset", "(Ljava/lang/String;)[B");
	if (read_remapping == nullptr || env->ExceptionCheck ()) {
		Helpers::abort_application ("Unable to find the JNI remapping asset reader");
	}
	jstring rid = env->NewStringUTF (Constants::runtime_identifier.data ());
	if (rid == nullptr || env->ExceptionCheck ()) {
		Helpers::abort_application ("Unable to create the runtime identifier");
	}
	auto asset = static_cast<jbyteArray>(env->CallStaticObjectMethod (config, read_remapping, rid));
	if (asset == nullptr || env->ExceptionCheck ()) {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		Helpers::abort_application ("Unable to load the JNI remapping asset");
	}
	jsize asset_length = env->GetArrayLength (asset);
	if (asset_length <= 0) {
		Helpers::abort_application ("Invalid JNI remapping asset length");
	}
	remapping_length = static_cast<size_t>(asset_length);
	remapping_bytes = static_cast<uint8_t*>(std::malloc (remapping_length));
	if (remapping_bytes == nullptr) {
		Helpers::abort_application ("Unable to allocate JNI remapping asset");
	}
	env->GetByteArrayRegion (asset, 0, asset_length, reinterpret_cast<jbyte*>(remapping_bytes));
	if (env->ExceptionCheck ()) {
		Helpers::abort_application ("Unable to copy the JNI remapping asset");
	}
	env->DeleteLocalRef (asset);
	env->DeleteLocalRef (rid);
	env->DeleteLocalRef (config);
	initialized = true;
}

auto AppBootstrapProperties::lookup (const char *name, size_t &value_length) noexcept -> const char*
{
	value_length = 0;
	for (Entry const& property : entries ()) {
		if (std::strcmp (name, property.name) == 0) {
			value_length = std::strlen (property.value);
			return property.value;
		}
	}
	return nullptr;
}

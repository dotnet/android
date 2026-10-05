#include <cstring>

#include <constants.hh>
#include <host/host-common.hh>
#include <host/os-bridge.hh>
#include <runtime-base/android-system.hh>
#include <shared/log_functions.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

void HostCommon::init_logging_categories () noexcept
{
	char value[Constants::PROPERTY_VALUE_BUFFER_LEN];
	const char *categories = AndroidSystem::monodroid_get_system_property (Constants::DEBUG_DOTNET_LOG_PROPERTY.data (), value, sizeof (value));
	if (categories == nullptr) {
		return;
	}

	// The value may point at immortal bundled property data. Bound comparisons by the parameter length.
	const char *param = categories;
	while (param != nullptr && *param != '\0') {
		const char *separator = strchr (param, ',');
		size_t param_length = separator != nullptr ? static_cast<size_t>(separator - param) : strlen (param);

		if (param_length == 3 && strncmp (param, "all", param_length) == 0) {
			log_categories = 0xFFFFFFFF;
			break;
		}

		if (param_length == 8 && strncmp (param, "assembly", param_length) == 0) {
			log_categories |= LOG_ASSEMBLY;
		} else if (param_length == 7 && strncmp (param, "default", param_length) == 0) {
			log_categories |= LOG_DEFAULT;
		} else if (param_length == 8 && strncmp (param, "debugger", param_length) == 0) {
			log_categories |= LOG_DEBUGGER;
		} else if (param_length == 2 && strncmp (param, "gc", param_length) == 0) {
			log_categories |= LOG_GC;
		} else if (param_length == 6 && strncmp (param, "timing", param_length) == 0) {
			log_categories |= LOG_TIMING;
		} else if (param_length == 7 && strncmp (param, "network", param_length) == 0) {
			log_categories |= LOG_NET;
		} else if (param_length == 7 && strncmp (param, "netlink", param_length) == 0) {
			log_categories |= LOG_NETLINK;
		}

		param = separator == nullptr ? nullptr : separator + 1;
	}
}

auto HostCommon::get_java_class_name_for_TypeManager (jclass klass) noexcept -> char*
{
	if (klass == nullptr || Class_getName == nullptr) {
		return nullptr;
	}

	JNIEnv *env = OSBridge::ensure_jnienv ();
	jstring name = reinterpret_cast<jstring> (env->CallObjectMethod (klass, Class_getName));
	if (name == nullptr) {
		log_errorf (LOG_DEFAULT, "Failed to obtain Java class name for object at %p", reinterpret_cast<void*>(klass));
		return nullptr;
	}

	const char *mutf8 = env->GetStringUTFChars (name, nullptr);
	if (mutf8 == nullptr) {
		log_errorf (LOG_DEFAULT, "Failed to convert Java class name to UTF8 (out of memory?)");
		env->DeleteLocalRef (name);
		return nullptr;
	}
	char *ret = strdup (mutf8);

	env->ReleaseStringUTFChars (name, mutf8);
	env->DeleteLocalRef (name);

	char *dot = strchr (ret, '.');
	while (dot != nullptr) {
		*dot = '/';
		dot = strchr (dot + 1, '.');
	}

	return ret;
}
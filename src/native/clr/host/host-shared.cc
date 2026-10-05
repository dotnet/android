#include <cstring>

#include <constants.hh>
#include <host/host-common.hh>
#include <host/os-bridge.hh>
#include <runtime-base/android-system.hh>
#include <shared/log_functions.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

namespace {
	[[gnu::always_inline]] void
	set_category (const char *name, const char *arg, size_t arg_length, unsigned int entry) noexcept
	{
		if ((log_categories & entry) == entry) {
			return;
		}

		if (strlen (name) == arg_length && strncmp (arg, name, arg_length) == 0) {
			log_categories |= entry;
		}
	}
}

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

		set_category ("assembly", param, param_length, LOG_ASSEMBLY);
		set_category ("default", param, param_length, LOG_DEFAULT);
		set_category ("debugger", param, param_length, LOG_DEBUGGER);
		set_category ("gc", param, param_length, LOG_GC);
		set_category ("timing", param, param_length, LOG_TIMING);
		set_category ("network", param, param_length, LOG_NET);
		set_category ("netlink", param, param_length, LOG_NETLINK);

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
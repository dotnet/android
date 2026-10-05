#include <cstring>

#include <constants.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

[[gnu::always_inline]] void
Logger::set_category (const char *name, const char *arg, size_t arg_length, unsigned int entry) noexcept
{
	if ((log_categories & entry) == entry) {
		return;
	}

	if (strlen (name) == arg_length && strncmp (arg, name, arg_length) == 0) {
		log_categories |= entry;
	}
}

void
Logger::init_logging_categories () noexcept
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
		set_category ("gc", param, param_length, LOG_GC);
		set_category ("timing", param, param_length, LOG_TIMING);
		set_category ("network", param, param_length, LOG_NET);

		param = separator == nullptr ? nullptr : separator + 1;
	}

}

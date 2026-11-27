#include <string_view>

#include <constants.hh>
#include <host/host-common.hh>
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

	// The value may point at immortal bundled property data. Parse without modifying it.
	std::string_view params { categories };
	while (!params.empty ()) {
		size_t separator = params.find (',');
		std::string_view param = params.substr (0, separator);

		if (param == "all") {
			log_categories = 0xFFFFFFFF;
			break;
		}

		if (param == "assembly") {
			log_categories |= LOG_ASSEMBLY;
		} else if (param == "default") {
			log_categories |= LOG_DEFAULT;
		} else if (param == "gc") {
			log_categories |= LOG_GC;
		} else if (param == "timing") {
			log_categories |= LOG_TIMING;
		} else if (param == "network") {
			log_categories |= LOG_NET;
		}

		if (separator == std::string_view::npos) {
			break;
		}
		params.remove_prefix (separator + 1);
	}
}

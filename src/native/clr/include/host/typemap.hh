#pragma once

#include <cstdint>
#include "../runtime-base/logger.hh"

namespace xamarin::android {
	class TypeMap
	{
	public:
		static auto typemap_managed_to_java ([[maybe_unused]] const char *typeName, [[maybe_unused]] const uint8_t *mvid) noexcept -> const char*
		{
			log_warn (LOG_ASSEMBLY, "{} not implemented yet", __PRETTY_FUNCTION__);
			log_warn (LOG_ASSEMBLY, "  asked for '{}'", optional_string (typeName));

			return nullptr;
		}
	};
}

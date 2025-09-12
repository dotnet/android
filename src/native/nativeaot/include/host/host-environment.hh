#pragma once

#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <string_view>

#include <runtime-base/logger.hh>

namespace xamarin::android {
	class HostEnvironment
	{
	public:
		static void init () noexcept;

		static void set_variable (const char *name, const char *value) noexcept
		{
			log_debug (LOG_DEFAULT, " Variable {} = '{}'", name, value);
			if (::setenv (name, value, 1) < 0) {
				log_warn (LOG_DEFAULT, "Failed to set environment variable '{}': {}", name, ::strerror (errno));
			}
		}

		static void set_variable (std::string_view const& name, std::string_view const& value) noexcept
		{
			set_variable (name.data (), value.data ());
		}
	};
}

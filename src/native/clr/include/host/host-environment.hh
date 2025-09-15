#pragma once

#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <string_view>

#include <runtime-base/logger.hh>

namespace xamarin::android {
	struct AppEnvironmentVariable {
		uint32_t name_index;
		uint32_t value_index;
	};

	class HostEnvironment
	{
	public:
		static void init () noexcept;

		[[gnu::flatten, gnu::always_inline]]
		static void set_variable (const char *name, const char *value) noexcept
		{
			log_debug (LOG_DEFAULT, " Variable {} = '{}'", optional_string (name), optional_string (value));
			if (::setenv (name, value, 1) < 0) {
				log_warn (LOG_DEFAULT, "Failed to set environment variable '{}': {}", name, ::strerror (errno));
			}
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_variable (std::string_view const& name, std::string_view const& value) noexcept
		{
			set_variable (name.data (), value.data ());
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_system_property (const char *name, const char *value) noexcept
		{
			// TODO: should we **actually** try to set the system property here? Would that even work? Needs testing
			log_debug (LOG_DEFAULT, " System property {} = '{}'", optional_string (name), optional_string (value));
		}

		[[gnu::flatten, gnu::always_inline]]
		static auto lookup_system_property (std::string_view const& name, size_t &value_len,
			uint32_t const count, AppEnvironmentVariable const (&entries)[],
			const char (&contents)[]) noexcept -> const char*
		{
			if (count == 0) {
				return nullptr;
			}

			for (size_t i = 0; i < count; i++) {
				AppEnvironmentVariable const& sys_prop = entries[i];
				const char *prop_name = &contents[sys_prop.name_index];
				if (name.compare (prop_name) != 0) {
					continue;
				}

				const char *prop_value = &contents[sys_prop.value_index];
				value_len = strlen (prop_value);
				return prop_value;
			}

			return nullptr;
		}

		template<void (*setter)(const char *name, const char *value) noexcept> [[gnu::flatten, gnu::always_inline]]
		static void set_values (uint32_t const& count, AppEnvironmentVariable const (&entries)[], const char (&contents)[]) noexcept
		{
			for (size_t i = 0; i < count; i++) {
				AppEnvironmentVariable const& env_var = entries[i];
				const char *var_name = &contents[env_var.name_index];
				const char *var_value = &contents[env_var.value_index];

				setter (var_name, var_value);
			}
		}
	};
}

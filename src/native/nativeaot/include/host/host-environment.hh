#pragma once

#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <string_view>

#include <runtime-base/logger.hh>

namespace xamarin::android {
<<<<<<< HEAD
	struct AppEnvironmentVariable {
		uint32_t name_index;
		uint32_t value_index;
	};

=======
>>>>>>> main
	class HostEnvironment
	{
	public:
		static void init () noexcept;

<<<<<<< HEAD
		[[gnu::flatten, gnu::always_inline]]
=======
>>>>>>> main
		static void set_variable (const char *name, const char *value) noexcept
		{
			log_debug (LOG_DEFAULT, " Variable {} = '{}'", name, value);
			if (::setenv (name, value, 1) < 0) {
				log_warn (LOG_DEFAULT, "Failed to set environment variable '{}': {}", name, ::strerror (errno));
			}
		}

<<<<<<< HEAD
		[[gnu::flatten, gnu::always_inline]]
=======
>>>>>>> main
		static void set_variable (std::string_view const& name, std::string_view const& value) noexcept
		{
			set_variable (name.data (), value.data ());
		}
<<<<<<< HEAD

		[[gnu::flatten, gnu::always_inline]]
		static void set_system_property (const char *name, const char *value) noexcept
		{
		}

	private:
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
=======
>>>>>>> main
	};
}

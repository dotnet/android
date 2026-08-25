#pragma once

#include <jni.h>

#include <cerrno>
#include <cstdlib>
#include <cstdlib>
#include <cstring>
#include <string_view>

#include <runtime-base/jni-wrappers.hh>
#include <runtime-base/logger.hh>
#include <runtime-base/util.hh>

struct AppEnvironmentVariable;

namespace xamarin::android {
	class HostEnvironment
	{
	public:
		static void init () noexcept;

		[[gnu::flatten, gnu::always_inline]]
		static void set_variable (const char *name, const char *value) noexcept
		{
			Util::set_environment_variable (name, value);
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_variable (std::string_view const& name, std::string_view const& value) noexcept
		{
			Util::set_environment_variable (name.data (), value.data ());
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_variable (std::string_view const& name, jstring_wrapper &value) noexcept
		{
			Util::set_environment_variable (name.data (), value);
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_variable_if_unset (std::string_view const& name, jstring_wrapper &value) noexcept
		{
			Util::set_environment_variable_if_unset (name, value);
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_system_property (const char *name, const char *value) noexcept
		{
			// TODO: should we **actually** try to set the system property here? Would that even work? Needs testing
			log_debugf (LOG_DEFAULT, " System property %s = '%s'", optional_string (name), optional_string (value));
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

	private:
		[[gnu::flatten, gnu::always_inline]]
		static void create_xdg_directory (jstring_wrapper &home, std::string_view const& relative_path, std::string_view const& environment_variable_name) noexcept
		{
			std::string_view home_path = home.get_string_view ();
			char stack_buffer [Util::LocalPathBufferSize];
			ssize_t result = Util::join_paths (stack_buffer, sizeof (stack_buffer), home_path, relative_path);
			abort_unless (result >= 0, "XDG directory path is too long");

			log_debugf (LOG_DEFAULT, "Creating XDG directory: %s", stack_buffer);
			int rv = Util::create_directory (stack_buffer, Constants::DEFAULT_DIRECTORY_MODE);
			if (rv < 0 && errno != EEXIST) {
				log_warnf (LOG_DEFAULT, "Failed to create XDG directory %s. %s", stack_buffer, strerror (errno));
			}

			if (!environment_variable_name.empty ()) {
				set_variable (environment_variable_name.data (), stack_buffer);
			}
		}

		[[gnu::flatten, gnu::always_inline]]
		static void create_xdg_directories_and_environment (jstring_wrapper &homeDir) noexcept
		{
			constexpr auto XDG_DATA_HOME = "XDG_DATA_HOME"sv;
			constexpr auto HOME_PATH = ".local/share"sv;
			create_xdg_directory (homeDir, HOME_PATH, XDG_DATA_HOME);

			constexpr auto XDG_CONFIG_HOME = "XDG_CONFIG_HOME"sv;
			constexpr auto CONFIG_PATH = ".config"sv;
			create_xdg_directory (homeDir, CONFIG_PATH, XDG_CONFIG_HOME);
		}

	public:
		[[gnu::flatten, gnu::always_inline]]
		static void setup_environment (jstring_wrapper &language, jstring_wrapper &files_dir, jstring_wrapper &cache_dir) noexcept
		{
			set_variable ("LANG"sv, language);
			Util::set_environment_variable_for_directory ("TMPDIR"sv, cache_dir);
			Util::set_environment_variable_for_directory ("HOME"sv, files_dir);
			create_xdg_directories_and_environment (files_dir);
		}
	};
}

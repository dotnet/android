#pragma once

#include <concepts>
#include <cstdio>
#include <string_view>

#include <sys/stat.h>

#include "../constants.hh"
#include "../shared/helpers.hh"
#include "jni-wrappers.hh"
#include "logger.hh"
#include "strings.hh"

namespace xamarin::android {
	namespace detail {
		template<typename T>
		concept PathComponentString = requires {
			std::same_as<std::remove_cvref_t<T>, char*> ||
			std::same_as<std::remove_cvref_t<T>, std::string_view> ||
			std::same_as<std::remove_cvref_t<T>, std::string>;
		};

		template<class T, size_t MaxBufferStorage>
		concept PathBuffer = requires {
			std::derived_from<std::remove_cvref<T>, dynamic_local_storage<MaxBufferStorage>> ||
			std::derived_from<std::remove_cvref<T>, static_local_storage<MaxBufferStorage>>;
		};
	}

	class Util
	{
	public:
		static int create_directory (const char *pathname, mode_t mode);
		static void create_public_directory (std::string_view const& dir);
		static auto monodroid_fopen (std::string_view const& filename, std::string_view const& mode) noexcept -> FILE*;
		static void set_world_accessable (std::string_view const& path);

		static auto should_log (LogCategories category) noexcept -> bool
		{
			return (log_categories & category) != 0;
		}

		static auto file_exists (const char *file) noexcept -> bool
		{
			if (file == nullptr) {
				return false;
			}

			struct stat s;
			if (::stat (file, &s) == 0 && (s.st_mode & S_IFMT) == S_IFREG) {
				return true;
			}
			return false;
		}

		template<size_t MaxStackSize>
		static auto file_exists (dynamic_local_string<MaxStackSize> const& file) noexcept -> bool
		{
			if (file.empty ()) {
				return false;
			}

			return file_exists (file.get ());
		}

		static void set_environment_variable (std::string_view const& name, jstring_wrapper& value) noexcept
		{
			::setenv (name.data (), value.get_cstr (), 1);
		}

		static void set_environment_variable_for_directory (std::string_view const& name, jstring_wrapper& value, bool createDirectory, mode_t mode) noexcept
		{
			if (createDirectory) {
				int rv = create_directory (value.get_cstr (), mode);
                if (rv < 0 && errno != EEXIST) {
                    log_warn (LOG_DEFAULT, "Failed to create directory '{}' for environment variable '{}'. {}", value.get_string_view (), name, strerror (errno));
				}
			}
			set_environment_variable (name, value);
		}

		static void set_environment_variable_for_directory (const char *name, jstring_wrapper &value) noexcept
		{
			set_environment_variable_for_directory (name, value, true, Constants::DEFAULT_DIRECTORY_MODE);
		}

	private:
		template<size_t MaxStackSpace, detail::PathBuffer<MaxStackSpace> TBuffer, detail::PathComponentString ...TPart>
		static void path_combine_common (TBuffer& buf, TPart&&... parts) noexcept
		{
			buf.clear ();

			for (auto const& part : {parts...}) {
				if (!buf.empty ()) {
					buf.append ("/"sv);
				}

				if constexpr (std::same_as<std::remove_cvref_t<decltype(part)>, char*>) {
					if (part != nullptr) {
						buf.append_c (part);
					}
				} else {
					buf.append (part);
				}
			}
		}

	public:
		template<size_t MaxStackSpace, detail::PathComponentString ...TParts>
		static void path_combine (dynamic_local_string<MaxStackSpace>& buf, TParts&&... parts) noexcept
		{
			path_combine_common<MaxStackSpace> (buf, std::forward<TParts>(parts)...);
		}
	};
}

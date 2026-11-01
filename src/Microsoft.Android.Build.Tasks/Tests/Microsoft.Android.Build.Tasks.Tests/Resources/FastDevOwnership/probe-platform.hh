#pragma once

#include <sys/stat.h>

#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <limits>
#include <optional>
#include <string_view>

using namespace std::literals;

namespace xamarin::android {
	constexpr int LOG_ASSEMBLY = 0;

	inline void log_debugf (int, const char*, ...) {}
	inline void log_infof (int, const char*, ...) {}

	inline void log_warnf (int, const char *format, ...)
	{
		va_list args;
		va_start (args, format);
		vfprintf (stderr, format, args);
		va_end (args);
		fputc ('\n', stderr);
	}

	class AndroidSystem
	{
	public:
		static auto get_primary_override_dir () noexcept -> const char*
		{
			return std::getenv ("XA_FASTDEV_TEST_DIR");
		}
	};

	class Helpers
	{
	public:
		template<typename T>
		static auto add_with_overflow_check (T left, T right) noexcept -> T
		{
			if (right > std::numeric_limits<T>::max () - left) {
				std::abort ();
			}
			return left + right;
		}
	};

	// Only platform discovery, filesystem queries and logging are substituted. Buffer loading,
	// retention, locking and TPA policy are compiled from the production source.
	class Util
	{
	public:
		static auto dir_exists (const char *path) noexcept -> bool
		{
			struct stat info;
			return stat (path, &info) == 0 && S_ISDIR (info.st_mode);
		}

		static auto file_exists (int fd, std::string_view name) noexcept -> bool
		{
			struct stat info;
			return fstatat (fd, name.data (), &info, 0) == 0 && S_ISREG (info.st_mode);
		}

		static auto get_file_size_at (int fd, std::string_view name) noexcept -> std::optional<size_t>
		{
			struct stat info;
			if (fstatat (fd, name.data (), &info, 0) != 0 || info.st_size < 0) {
				return std::nullopt;
			}
			return static_cast<size_t> (info.st_size);
		}
	};
}

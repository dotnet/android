#pragma once

#include <cstddef>
#include <cstdint>
#include <limits>
#include <optional>
#include <string_view>

using namespace std::literals;

extern const char *override_path;
extern size_t assembly_size;

namespace xamarin::android {
	constexpr int LOG_ASSEMBLY = 0;
	void log_warnf (int category, const char *format, ...);
	void log_debugf (int category, const char *format, ...);
	void log_infof (int category, const char *format, ...);

	struct AndroidSystem
	{
		static auto get_primary_override_dir () -> const char* { return override_path; }
	};

	struct Util
	{
		static auto dir_exists (const char*) -> bool { return true; }
		static auto file_exists (int, std::string_view) -> bool { return true; }
		static auto get_file_size_at (int, std::string_view) -> std::optional<size_t> { return assembly_size; }
	};

	struct Helpers
	{
		template<typename T>
		static auto add_with_overflow_check (T left, T right) -> T { return left + right; }
	};
}

#pragma once

#include <cstdio>
#include <string_view>

#include "logger.hh"

namespace xamarin::android {
	class Util
	{
	public:
		static void create_public_directory (std::string_view const& dir);
		static auto monodroid_fopen (std::string_view const& filename, std::string_view const& mode) noexcept -> FILE*;
		static void set_world_accessable (std::string_view const& path);

		static auto should_log (LogCategories category) noexcept -> bool
		{
			return (log_categories & category) != 0;
		}
	};
}

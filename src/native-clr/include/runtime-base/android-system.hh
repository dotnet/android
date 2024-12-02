#pragma once

#include <string_view>

#include "../constants.hh"
#include "../shared/log_types.hh"
#include "strings.hh"

namespace xamarin::android {
	class AndroidSystem
	{
	public:
		static auto get_max_gref_count () noexcept -> long
		{
			return max_gref_count;
		}

		static void init_max_gref_count () noexcept
		{
			max_gref_count = get_max_gref_count_from_system ();
		}

		static void set_running_in_emulator (bool yesno) noexcept
		{
			running_in_emulator = yesno;
		}

		static auto monodroid_get_system_property (std::string_view const& name, dynamic_local_string<Constants::PROPERTY_VALUE_BUFFER_LEN> &value) noexcept -> int;

	private:
		static auto lookup_system_property (std::string_view const &name, size_t &value_len) noexcept -> const char*;
		static auto monodroid__system_property_get (std::string_view const&, char *sp_value, size_t sp_value_len) noexcept -> int;
		static auto get_max_gref_count_from_system () noexcept -> long;

	private:
		static inline long max_gref_count = 0;
		static inline bool running_in_emulator = false;
	};
}

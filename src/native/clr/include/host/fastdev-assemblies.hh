#pragma once

#include <dirent.h>

#include <cstdint>
#include <mutex>
#include <string>
#include <string_view>

namespace xamarin::android {
	class FastDevAssemblies
	{
	public:
#if defined(DEBUG)
		static auto open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*;
		static auto build_tpa_list (std::string &tpa_list) noexcept -> bool;
#else
		static auto open_assembly ([[maybe_unused]] std::string_view const& name, [[maybe_unused]]  int64_t &size) noexcept -> void*
		{
			return nullptr;
		}

		static auto build_tpa_list ([[maybe_unused]] std::string &tpa_list) noexcept -> bool
		{
			return false;
		}
#endif

	private:
#if defined(DEBUG)
		static inline DIR *override_dir = nullptr;
		static inline int override_dir_fd = -1;
		static inline std::mutex override_dir_lock {};
#endif
	};
}

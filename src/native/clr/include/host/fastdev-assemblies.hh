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
		// Set by `build_tpa_list` when assemblies in the override directory are
		// passed to CoreCLR via `TRUSTED_PLATFORM_ASSEMBLIES`. When true, the
		// external assembly probe yields to TPA-based loading so that
		// `Assembly.Location` is populated with the full disk path (needed for
		// `StackTraceSymbols` to find sibling portable PDB files).
	public:
		static inline bool tpa_in_use = false;
#endif
	};
}

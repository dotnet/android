#pragma once

#include <dirent.h>

#include <cstdint>
#include <string_view>

#include <runtime-base/mutex.hh>

namespace xamarin::android {
	class FastDevAssemblies
	{
	public:
#if defined(DEBUG)
		static auto open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*;

		// Builds the `TRUSTED_PLATFORM_ASSEMBLIES` list from the override directory. Returns a
		// `malloc`ed, NUL-terminated, `:`-separated list of absolute paths which the caller owns,
		// or `nullptr` when no usable list could be built.
		static auto build_tpa_list () noexcept -> char*;

		// Frees a list returned by `build_tpa_list ()` and puts assembly loading back on the
		// probe-only path. Callers use this instead of touching `tpa_in_use` directly, which does
		// not exist in Release builds.
		static void discard_tpa_list (char *tpa_list) noexcept;
#else
		static auto open_assembly ([[maybe_unused]] std::string_view const& name, [[maybe_unused]]  int64_t &size) noexcept -> void*
		{
			return nullptr;
		}

		static auto build_tpa_list () noexcept -> char*
		{
			return nullptr;
		}

		static void discard_tpa_list ([[maybe_unused]] char *tpa_list) noexcept
		{
		}
#endif

	private:
#if defined(DEBUG)
		static inline DIR *override_dir = nullptr;
		static inline int override_dir_fd = -1;
		static inline pthread_mutex_t override_dir_lock = PTHREAD_MUTEX_INITIALIZER;
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

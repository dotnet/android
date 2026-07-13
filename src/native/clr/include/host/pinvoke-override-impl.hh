#pragma once

#if !defined(PINVOKE_OVERRIDE_INLINE)
#error The PINVOKE_OVERRIDE_INLINE macro must be defined before including this header file
#endif

#include "pinvoke-override.hh"
#include "../runtime-base/logger.hh"
#include "../runtime-base/monodroid-dl.hh"

namespace xamarin::android {
	PINVOKE_OVERRIDE_INLINE
	auto PinvokeOverride::load_library_symbol (std::string_view const& library_name, std::string_view const& symbol_name) noexcept -> void*
	{
		void *lib_handle = nullptr;

		// We're being called as part of the p/invoke mechanism, so skip AOT DSO lookup.
		constexpr bool PREFER_AOT_CACHE = false;

		// Handle p/invokes of the form [DllImport ("liblog")] or [DllImport ("log")]
		// TODO: try modifying the name to contain both the `log` prefix and the `.so` suffix
		dynamic_local_path_string short_library_name;
		if (!Util::path_has_directory_components (library_name)) {
			if (!library_name.starts_with (Constants::DSO_PREFIX)) {
				short_library_name.append (Constants::DSO_PREFIX);
			}
			short_library_name.append (library_name);
			if (!short_library_name.ends_with (Constants::dso_suffix)) {
				short_library_name.append (Constants::dso_suffix);
			}

			log_debug (LOG_ASSEMBLY, "Modified p/invoke library name to '{}'", short_library_name.get ());
			lib_handle = MonodroidDl::monodroid_dlopen<PREFER_AOT_CACHE> (short_library_name.get (), microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY);
		}

		if (lib_handle == nullptr) {
			lib_handle = MonodroidDl::monodroid_dlopen<PREFER_AOT_CACHE> (library_name, microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY);
		}

		if (lib_handle == nullptr) {
			log_warn (LOG_ASSEMBLY, "Shared library '{}' not loaded, p/invoke '{}' may fail", library_name, symbol_name);
			return nullptr;
		}

		void *entry_handle = MonodroidDl::monodroid_dlsym (lib_handle, symbol_name);
		if (entry_handle == nullptr) {
			log_warn (LOG_ASSEMBLY, "Symbol '{}' not found in shared library '{}', p/invoke may fail", symbol_name, library_name);
			return nullptr;
		}

		return entry_handle;
	}
}

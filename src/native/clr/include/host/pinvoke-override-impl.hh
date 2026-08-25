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

		// Handle p/invokes of the form [DllImport ("liblog")] or [DllImport ("log")]
		if (!Util::path_has_directory_components (library_name)) {
			char stack_buffer [Util::LocalPathBufferSize];
			char *heap_buffer;
			const char *short_library_name = Util::format_dso_name (stack_buffer, sizeof (stack_buffer), heap_buffer, library_name, true);

			std::string_view short_library_name_view { short_library_name };
			log_debug (LOG_ASSEMBLY, "Modified p/invoke library name to '{}'", short_library_name_view);
			lib_handle = MonodroidDl::monodroid_dlopen (short_library_name_view, microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY);
			std::free (heap_buffer);
		}

		if (lib_handle == nullptr) {
			lib_handle = MonodroidDl::monodroid_dlopen (library_name, microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY);
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

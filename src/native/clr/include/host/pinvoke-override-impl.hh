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
			char *short_library_name = Util::format_dso_name (stack_buffer, sizeof (stack_buffer), library_name, true);

			std::string_view short_library_name_view { short_library_name };
			log_debugf (LOG_ASSEMBLY, "Modified p/invoke library name to '%s'", short_library_name);
			lib_handle = MonodroidDl::monodroid_dlopen (short_library_name_view, microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY);
			if (short_library_name != stack_buffer) {
				std::free (short_library_name);
			}
		}

		if (lib_handle == nullptr) {
			lib_handle = MonodroidDl::monodroid_dlopen (library_name, microsoft::java_interop::JAVA_INTEROP_LIB_LOAD_LOCALLY);
		}

		if (lib_handle == nullptr) {
			log_warnf (LOG_ASSEMBLY, "Shared library '%.*s' not loaded, p/invoke '%.*s' may fail", static_cast<int>(library_name.length ()), library_name.data (), static_cast<int>(symbol_name.length ()), symbol_name.data ());
			return nullptr;
		}

		void *entry_handle = MonodroidDl::monodroid_dlsym (lib_handle, symbol_name);
		if (entry_handle == nullptr) {
			log_warnf (LOG_ASSEMBLY, "Symbol '%.*s' not found in shared library '%.*s', p/invoke may fail", static_cast<int>(symbol_name.length ()), symbol_name.data (), static_cast<int>(library_name.length ()), library_name.data ());
			return nullptr;
		}

		return entry_handle;
	}
}

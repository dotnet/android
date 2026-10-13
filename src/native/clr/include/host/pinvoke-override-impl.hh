#pragma once

#include <dlfcn.h>

#include "pinvoke-override.hh"
#include "../runtime-base/monodroid-dl.hh"
#include <shared/log_functions.hh>

namespace xamarin::android {
	auto PinvokeOverride::load_symbol (const char *library_name, const char *symbol_name) noexcept -> const void*
	{
		log_debugf (LOG_ASSEMBLY, "load_symbol (\"%s\", \"%s\")", optional_string (library_name), optional_string (symbol_name));
		if (library_name == nullptr || symbol_name == nullptr) {
			return nullptr;
		}

		void *lib_handle = nullptr;

		// Handle p/invokes of the form [DllImport ("liblog")] or [DllImport ("log")]
		if (!Util::path_has_directory_components (library_name)) {
			char stack_buffer [Util::LocalPathBufferSize];
			char *short_library_name = Util::format_dso_name (stack_buffer, sizeof (stack_buffer), library_name, true);

			log_debugf (LOG_ASSEMBLY, "Modified p/invoke library name to '%s'", short_library_name);
			lib_handle = MonodroidDl::monodroid_dlopen (short_library_name, RTLD_NOW | RTLD_LOCAL);
			if (short_library_name != stack_buffer) {
				std::free (short_library_name);
			}
		}

		if (lib_handle == nullptr) {
			lib_handle = MonodroidDl::monodroid_dlopen (library_name, RTLD_NOW | RTLD_LOCAL);
		}

		if (lib_handle == nullptr) {
			log_warnf (LOG_ASSEMBLY, "Shared library '%s' not loaded, p/invoke '%s' may fail", library_name, symbol_name);
			log_debugf (LOG_DEFAULT, "p/invoke not found");
			return nullptr;
		}

		dlerror ();
		void *entry_handle = dlsym (lib_handle, symbol_name);
		if (entry_handle == nullptr) {
			const char *error = dlerror ();
			log_errorf (
				LOG_ASSEMBLY,
				"Could not find symbol '%s' in shared library '%s': %s",
				symbol_name,
				library_name,
				optional_string (error, "Unknown error")
			);
			log_debugf (LOG_DEFAULT, "p/invoke not found");
			return nullptr;
		}

		log_debugf (LOG_DEFAULT, "p/invoke found");
		return entry_handle;
	}
}

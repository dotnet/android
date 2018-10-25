// Dear Emacs, this is a -*- C++ -*- header
#ifndef INC_MONODROID_EMBEDDED_ASSEMBLIES_H
#define INC_MONODROID_EMBEDDED_ASSEMBLIES_H

#include "dylib-mono.h"

/* filename is e.g. System.dll, System.dll.mdb, System.pdb */
typedef int (*monodroid_should_register)(const char *filename, void *user_data);

/* invoked for each assembly/debug symbol file to determine if the apk_file entry should be used */
MONO_API void monodroid_embedded_assemblies_set_should_register (
		monodroid_should_register   should_register,
		void                       *user_data
);

/* mono_bool; returns previous value */
MONO_API int monodroid_embedded_assemblies_set_register_debug_symbols (
		int /* mono_bool */   register_debug_symbols
);

/* returns current number of *all* assemblies found from all invocations */
MONO_API int monodroid_embedded_assemblies_register_from (
		xamarin::android::DylibMono        *imports,
		const char       *apk_file
);

/* mono_bool; returns TRUE if the install succeeded. */
MONO_API int monodroid_embedded_assemblies_install_preload_hook (xamarin::android::DylibMono *imports);

int try_load_typemaps_from_directory (const char *path);

#endif /* INC_MONODROID_EMBEDDED_ASSEMBLIES_H */

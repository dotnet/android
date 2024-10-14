#include <cstdarg>
#include <cstring>
#include <android/set_abort_message.h>

#include "helpers.hh"
#include "java-interop-logger.h"

using namespace xamarin::android;

[[noreturn]] void
Helpers::abort_application (LogCategories category, const char *message, bool log_location, std::source_location sloc) noexcept
{
	// Log it, but also...
	log_fatal (category, message);

	// ...let android include it in the tombstone, debuggerd output, stack trace etc
	android_set_abort_message (message);

	if (log_location) {
		// We don't want to log the full path, just the file name.  libc++ uses full file path here.
		const char *file_name = sloc.file_name ();
		const char *last_path_sep = strrchr (file_name, '/');

		if (last_path_sep == nullptr) [[unlikely]] {
			// In case we were built on Windows
			last_path_sep = strrchr (file_name, '\\');
		}

		if (last_path_sep != nullptr) [[likely]] {
			last_path_sep++;
			if (*last_path_sep != '\0') [[unlikely]] {
				file_name = last_path_sep;
			}
		}

		log_fatal (
			category,
			"Abort at %s:%u:%u ('%s')",
			file_name,
			sloc.line (),
			sloc.column (),
			sloc.function_name ()
		);
	}
	std::abort ();
}

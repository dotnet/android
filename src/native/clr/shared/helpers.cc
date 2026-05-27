#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <android/set_abort_message.h>

#include <shared/helpers.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

[[noreturn]] void
Helpers::abort_applicationf (LogCategories category, std::source_location sloc, const char *format, ...) noexcept
{
	char message[512];
	va_list args;
	const char *safe_format = format == nullptr ? "<null>" : format;
	va_start (args, format);
	vsnprintf (message, sizeof (message), safe_format, args);
	va_end (args);

	abort_application (category, message, true, sloc);
}

[[noreturn]] void
Helpers::abort_application (LogCategories category, const char *message, bool log_location, std::source_location sloc) noexcept
{
	// Log it, but also...
	log_write (category, LogLevel::Fatal, message);

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

		log_write_fmt (
			category,
			LogLevel::Fatal,
			"Abort at %s:%u:%u ('%s')",
			file_name,
			sloc.line (),
			sloc.column (),
			sloc.function_name ()
		);
	}
	std::abort ();
}

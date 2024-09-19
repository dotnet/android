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
		log_fatal (
			category,
			"Abort at %s:%u:%u ('%s')",
			sloc.file_name (),
			sloc.line (),
			sloc.column (),
			sloc.function_name ()
		);
	}
	std::abort ();
}

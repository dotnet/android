#include "helpers.hh"

using namespace xamarin::android;

[[noreturn]] void
Helpers::abort_application (bool log_location, std::source_location sloc) noexcept
{
	if (log_location) {
		log_fatal (
			LOG_DEFAULT,
			"Abort at %s:%u:%u ('%s')",
			sloc.file_name (),
			sloc.line (),
			sloc.column (),
			sloc.function_name ()
		);
	}
	std::abort ();
}

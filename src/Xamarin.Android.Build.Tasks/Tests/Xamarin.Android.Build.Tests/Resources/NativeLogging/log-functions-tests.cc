#include <cstdarg>
#include <cstdio>
#include <cstring>

#include <android/log.h>
#include <shared/log_types.hh>

namespace {
	constexpr size_t MESSAGE_SIZE = 256;

	char last_message [MESSAGE_SIZE];
	int last_priority;
	int log_count;

	auto fail (const char *message) noexcept -> int
	{
		std::fprintf (stderr, "%s\n", message);
		return 1;
	}

	void reset_log () noexcept
	{
		std::memset (last_message, 0, sizeof (last_message));
		last_priority = -1;
		log_count = 0;
	}
}

extern "C" int
__android_log_write (int priority, const char*, const char *text)
{
	last_priority = priority;
	log_count++;
	return std::snprintf (last_message, sizeof (last_message), "%s", text);
}

extern "C" int
__android_log_vprint (int priority, const char*, const char *format, va_list args)
{
	last_priority = priority;
	log_count++;
	return std::vsnprintf (last_message, sizeof (last_message), format, args);
}

int
main ()
{
	using namespace xamarin::android;

	const char *text = "forwarded";
	const void *pointer = &log_count;
	char expected [MESSAGE_SIZE];
	std::snprintf (expected, sizeof (expected), "%s %p %d", text, pointer, -42);

	reset_log ();
	log_writef (LOG_ASSEMBLY, LogLevel::Info, "%s %p %d", text, pointer, -42);
	if (log_count != 1 || last_priority != ANDROID_LOG_INFO || std::strcmp (last_message, expected) != 0) {
		return fail ("printf arguments were not forwarded to the Android logging API");
	}

	log_categories = LOG_NONE;
	reset_log ();
	log_debugf (LOG_ASSEMBLY, "%s", "disabled debug");
	log_infof (LOG_ASSEMBLY, "%s", "disabled info");
	if (log_count != 0 || last_message[0] != '\0') {
		return fail ("disabled debug or info logging invoked formatting");
	}

	log_categories = LOG_ASSEMBLY;
	reset_log ();
	log_debugf (LOG_ASSEMBLY, "%d", 7);
	if (log_count != 1 || last_priority != ANDROID_LOG_DEBUG || std::strcmp (last_message, "7") != 0) {
		return fail ("enabled debug logging did not format its message");
	}

	reset_log ();
	log_infof (LOG_ASSEMBLY, "%s", "enabled info");
	if (log_count != 1 || last_priority != ANDROID_LOG_INFO || std::strcmp (last_message, "enabled info") != 0) {
		return fail ("enabled info logging did not format its message");
	}

	return 0;
}

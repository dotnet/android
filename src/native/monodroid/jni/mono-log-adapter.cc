#include <android/log.h>
#include <mono/utils/mono-logger.h>

#include "monodroid-glue-internal.hh"
#include "strings.hh"
#include "logger.hh"

using namespace xamarin::android::internal;

void
MonodroidRuntime::mono_log_handler (const char *log_domain, const char *log_level, const char *message, mono_bool fatal, [[maybe_unused]] void *user_data)
{
	android_LogPriority prio = ANDROID_LOG_DEFAULT;

	if (log_level != nullptr && *log_level != '\0') {
		switch (*log_level) {
			case 'e':
				prio = ANDROID_LOG_ERROR;
				break;

			case 'c':
				prio = ANDROID_LOG_FATAL;
				break;

			case 'w':
				prio = ANDROID_LOG_WARN;
				break;

			case 'u':
			case 'm':
				prio = ANDROID_LOG_VERBOSE;
				break;

			case 'i':
				prio = ANDROID_LOG_INFO;
				break;

			case 'd':
				prio = ANDROID_LOG_DEBUG;
				break;
		}
	}

	__android_log_write (prio, log_domain, message);
	if (fatal) {
		Helpers::abort_application ();
	}
}

void
MonodroidRuntime::mono_log_standard_streams_handler (const char *str, mono_bool is_stdout)
{
	static constexpr char mono_stdout[] = "mono-stdout";
	static constexpr char mono_stderr[] = "mono-stderr";

	__android_log_write (ANDROID_LOG_DEFAULT, is_stdout ? mono_stdout : mono_stderr, str);
}

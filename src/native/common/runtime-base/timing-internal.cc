#include <chrono>
#include <cstdlib>

#include <runtime-base/android-system.hh>
#include <runtime-base/timing-internal.hh>
#include <runtime-base/util.hh>

namespace xamarin::android {
	FastTiming internal_timing;
}

using namespace xamarin::android;
using namespace std::literals;

namespace chrono = std::chrono;

void FastTiming::really_initialize (bool log_immediately) noexcept
{
	internal_timing.configure_for_use ();
	immediate_logging = log_immediately;

	// TLS variables are initialized on first use, do it here so that we can have
	// the overhead out of mind later, at least for the main thread.
	push_sequence_event (nullptr);
	pop_sequence_event ();

	// Options in `debug.mono.timing` are relevant only when immediate logging is disabled
	if (immediate_logging) {
		return;
	}

	char value [Constants::PROPERTY_VALUE_BUFFER_LEN];
	const char *options = AndroidSystem::monodroid_get_system_property (Constants::DEBUG_MONO_TIMING.data (), value, sizeof (value));
	if (options != nullptr) {
		internal_timing.parse_options (options);
	}

	log_write (
		LOG_TIMING,
		LogLevel::Info,
		"[2/1] To get timing results, send the mono.android.app.DUMP_TIMING_DATA intent to the application"
	);
}

void FastTiming::parse_options (const char *options) noexcept
{
	const char *param = options;
	while (param != nullptr && *param != '\0') {
		// The value may point at immortal bundled property data, so the parameters cannot be
		// NUL-terminated in place. Bound every comparison by `param_length` instead.
		const char *separator = strchr (param, ',');
		size_t param_length = separator != nullptr ? static_cast<size_t>(separator - param) : strlen (param);

		if (param_length == OPT_TO_FILE.length () && strncmp (param, OPT_TO_FILE.data (), param_length) == 0) {
			log_to_file = true;
		} else if (param_length >= OPT_FILE_NAME.length () && strncmp (param, OPT_FILE_NAME.data (), OPT_FILE_NAME.length ()) == 0) {
			output_file_name = std::make_unique<std::string> (param + OPT_FILE_NAME.length (), param_length - OPT_FILE_NAME.length ());
		} else if (param_length >= OPT_DURATION.length () && strncmp (param, OPT_DURATION.data (), OPT_DURATION.length ()) == 0) {
			const char *duration = param + OPT_DURATION.length ();
			char *end;
			errno = 0;
			unsigned long long parsed_duration = strtoull (duration, &end, 10);
			if (end == duration || end != param + param_length || errno == ERANGE || parsed_duration > std::numeric_limits<size_t>::max ()) {
				log_warnf (LOG_TIMING, "Failed to parse duration in milliseconds from '%.*s'", static_cast<int>(param_length), param);
				duration_ms = default_duration_milliseconds;
			} else {
				duration_ms = static_cast<size_t>(parsed_duration);
			}
		}

		param = separator == nullptr ? nullptr : separator + 1;
	}

	if (output_file_name) {
		log_to_file = true;
	}

	// If logging to file is requested, turn off immediate logging.
	if (log_to_file) {
		immediate_logging = false;
	}
}

bool FastTiming::no_events_logged (size_t entries) noexcept
{
	if (entries > 0) {
		return false;
	}

	log_write (LOG_TIMING, LogLevel::Info, "[2/3] No events logged");
	return true;
}

void FastTiming::dump (size_t entries, bool indent, std::function<void(std::string_view const&)> line_writer) noexcept
{
	char stack_buffer [Constants::MAX_LOGCAT_MESSAGE_LENGTH];

	line_writer ("Startup costs:"sv);
	auto log = [&] (TimingEvent const& event) -> uint64_t {
		size_t message_length;
		char *message = build_message (event, stack_buffer, sizeof (stack_buffer), &message_length, indent);
		line_writer (std::string_view { message, message_length });
		if (message != stack_buffer) {
			std::free (message);
		}
		return event_duration_ns (event);
	};
	log (start_end_event_time);
	log (get_time_overhead);
	log (init_time);
	line_writer (Constants::EMPTY);

	// Values are in nanoseconds
	uint64_t total_assembly_load_time = 0u;
	uint64_t total_java_to_managed_time = 0u;
	uint64_t total_managed_to_java_time = 0u;
	uint64_t total_assembly_decompression_time = 0u;

	line_writer ("All logged events:"sv);
	for (size_t i = 0uz; i < entries; i++) {
		TimingEvent const& event = get_event (i);
		if (!__atomic_load_n (&event.complete, __ATOMIC_ACQUIRE)) {
			continue;
		}
		uint64_t event_time_ns = log (event);

		switch (event.kind) {
			case TimingEventKind::AssemblyLoad:
				total_assembly_load_time += event_time_ns;
				break;

			case TimingEventKind::AssemblyDecompression:
				total_assembly_decompression_time += event_time_ns;
				break;

			case TimingEventKind::JavaToManaged:
				total_java_to_managed_time += event_time_ns;
				break;

			case TimingEventKind::ManagedToJava:
				total_managed_to_java_time += event_time_ns;
				break;

			default:
				// Ignore other kinds
				break;
		}
	}

	line_writer (Constants::EMPTY);
	line_writer ("[2/4] Accumulated performance results"sv);

	auto log_time = [&line_writer] (std::string_view const& msg, uint64_t ns)
	{
		chrono::nanoseconds time_ns (ns);
		// Do not change the string format after the first colon, its format is required by performance measuring
		// utilities.
		auto format_time = [&] (char *buffer, size_t buffer_size) noexcept -> int {
			return snprintf (
				buffer,
				buffer_size,
				"  %.*s: %lld:%lld::%lld",
				static_cast<int>(msg.length ()),
				msg.data (),
				static_cast<long long>(chrono::duration_cast<chrono::seconds> (time_ns).count ()),
				static_cast<long long>(chrono::duration_cast<chrono::milliseconds> (time_ns).count ()),
				static_cast<long long>((time_ns % 1ms).count ())
			);
		};

		// Formatted into `stack_buffer`, falling back to a heap buffer when the message doesn't fit.
		char stack_buffer [Constants::MAX_LOGCAT_MESSAGE_LENGTH];
		char *buffer = stack_buffer;
		int result = format_time (stack_buffer, sizeof (stack_buffer));
		abort_unless (result >= 0, "Failed to format the accumulated timing results");

		size_t length = static_cast<size_t>(result);
		if (length >= sizeof (stack_buffer)) {
			size_t required_capacity = length + 1uz;
			buffer = static_cast<char*> (std::malloc (required_capacity));
			abort_unless (buffer != nullptr, "Failed to allocate the accumulated timing results message");
			result = format_time (buffer, required_capacity);
			abort_unless (
				result >= 0 && static_cast<size_t>(result) == length,
				"Failed to format the accumulated timing results using the required capacity"
			);
		}

		line_writer (std::string_view { buffer, length });
		if (buffer != stack_buffer) {
			std::free (buffer);
		}
	};

	// Do not change the sequence numbers. If a measurement is removed, its sequence number must not be reused.
	// The sequence numbers are used by performance measuring utilities to find the figures.
	log_time ("[2/5] Assembly load"sv, total_assembly_load_time);
	log_time ("[2/6] Java to Managed lookup"sv, total_java_to_managed_time);
	log_time ("[2/7] Managed to Java lookup"sv, total_managed_to_java_time);
	log_time ("[2/8] Assembly decompression"sv, total_assembly_decompression_time);
}

void FastTiming::dump_to_logcat (size_t entries) noexcept
{
	log_write (LOG_TIMING, LogLevel::Info, "[2/2] Performance measurement results");
	if (no_events_logged (entries)) {
		return;
	}

	auto line_writer = [](std::string_view const& msg) {
		// Don't add empty messages to the logcat, waste of time
		if (msg.empty ()) {
			return;
		}
		log_writef (LOG_TIMING, LogLevel::Info, "%.*s", static_cast<int>(msg.length ()), msg.data ());
	};
	dump (entries, true /* indent */, line_writer);
}

void FastTiming::dump_to_file (size_t entries) noexcept
{
	if (no_events_logged (entries)) {
		return;
	}

	// TMPDIR is normally set by us at startup.
	// Note that to access the file for a release app, the app must be made debuggable
	// and `run-as` must be used.
	const char *temporary_directory = getenv ("TMPDIR");
	if (temporary_directory == nullptr || *temporary_directory == '\0') {
		log_errorf (LOG_TIMING, "[2/2] Unable to create the performance measurements file: TMPDIR is not set");
		return;
	}

	std::string_view file_name = output_file_name == nullptr ? default_timing_file_name : *output_file_name;
	char stack_buffer [Util::LocalPathBufferSize];
	char *timing_log_path = Util::join_paths (stack_buffer, sizeof (stack_buffer), temporary_directory, file_name);

	FILE *timing_log = Util::monodroid_fopen (timing_log_path, "w");
	if (timing_log == nullptr) {
		log_errorf (LOG_TIMING, "[2/2] Unable to create the performance measurements file '%s'", timing_log_path);
		if (timing_log_path != stack_buffer) {
			std::free (timing_log_path);
		}
		return;
	}

	if (!Util::set_world_accessible (fileno (timing_log))) {
		log_warnf (LOG_TIMING, "[2/2] Failed to make performance measurements file '%s' world-readable", timing_log_path);
		fclose (timing_log);
		if (timing_log_path != stack_buffer) {
			std::free (timing_log_path);
		}
		return;
	}

	log_infof (LOG_TIMING, "[2/2] Performance measurement results logged to file: %s", timing_log_path);

	auto line_writer = [=](std::string_view const& msg) {
		if (!msg.empty ()) {
			fwrite (msg.data (), msg.size (), 1, timing_log);
		}
		fwrite (Constants::NEWLINE.data (), Constants::NEWLINE.size (), 1, timing_log);
	};

	dump (entries, true /* indent */, line_writer);
	fflush (timing_log);
	fclose (timing_log);
	if (timing_log_path != stack_buffer) {
		std::free (timing_log_path);
	}
}

void FastTiming::dump () noexcept
{
	if (immediate_logging) {
		return;
	}

	size_t entries = next_event_index.load ();
	if (log_to_file) {
		dump_to_file (entries);
	} else {
		dump_to_logcat (entries);
	}
}

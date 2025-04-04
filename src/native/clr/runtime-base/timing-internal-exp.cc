#include <chrono>

#include <constants.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/startup-aware-lock.hh>
#include <runtime-base/strings.hh>
#include <runtime-base/timing-internal-exp.hh>
#include <runtime-base/util.hh>

namespace xamarin::android::exp {
	FastTiming internal_timing;
}

using namespace xamarin::android::exp;
using namespace std::literals;

namespace chrono = std::chrono;

void FastTiming::really_initialize (bool log_immediately) noexcept
{
	internal_timing.configure_for_use ();
	is_enabled = true;
	immediate_logging = log_immediately;

	// TLS variables are initialized on first use, do it here so that we can have
	// the overhead out of mind later.
	open_sequences.push (0);
	open_sequences.pop ();

	dynamic_local_string<Constants::PROPERTY_VALUE_BUFFER_LEN> value;
	if (AndroidSystem::monodroid_get_system_property (Constants::DEBUG_MONO_TIMING, value) != 0) {
		internal_timing.parse_options (value);
	}

	if (immediate_logging) {
		return;
	}

	log_write (
		LOG_TIMING,
		LogLevel::Info,
		"[2/1] To get timing results, send the mono.android.app.DUMP_TIMING_DATA intent to the application"sv
	);
}

void FastTiming::parse_options (dynamic_local_string<Constants::PROPERTY_VALUE_BUFFER_LEN> const& value) noexcept
{
	if (value.length () == 0) {
		return;
	}

	string_segment param;
	while (value.next_token (',', param)) {
		if (param.equal (OPT_TO_FILE)) {
			log_to_file = true;
			continue;
		}

		if (param.starts_with (OPT_FILE_NAME)) {
			output_file_name = std::make_unique<std::string> (param.start () + OPT_FILE_NAME.length (), param.length () - OPT_FILE_NAME.length ());
			continue;
		}

		if (param.starts_with (OPT_DURATION)) {
			if (!param.to_integer (duration_ms, OPT_DURATION.length ())) {
				log_warn (LOG_TIMING, "Failed to parse duration in milliseconds from '%s'", param.start ());
				duration_ms = default_duration_milliseconds;
			}
			continue;
		}
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

	log_write (LOG_TIMING, LogLevel::Info, "[2/3] No events logged"sv);
	return true;
}

template<void(line_writer)(std::string_view const& buffer)>
void FastTiming::dump (size_t entries, bool indent) noexcept
{
	dynamic_local_string<Constants::MAX_LOGCAT_MESSAGE_LENGTH, char> message;

	// Values are in nanoseconds
	uint64_t total_assembly_load_time = 0u;
	uint64_t total_java_to_managed_time = 0u;
	uint64_t total_managed_to_java_time = 0u;
	uint64_t total_assembly_decompression_time = 0u;
	uint64_t event_time_ns;

	format_message (init_time, message, indent);
	for (size_t i = 0uz; i < entries; i++) {
		TimingEvent const& event = events[i];
		event_time_ns = format_message (event, message, indent);
		line_writer (message.as_string_view ());

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

	line_writer ("[2/4] Accumulated performance results"sv);

	auto log_time = [] (std::string_view const& msg, uint64_t ns)
	{
		chrono::nanoseconds time_ns (ns);
		// Do not change the string format after the first colon, its format is required by performance measuring
		// utilities.
		// TODO: it's a bit wasteful... if dynamic_local_string is made an output iterator, we can use std::format_to
		std::string s = std::format (
			"  {}: {}:{}::{}",
			msg,
			chrono::duration_cast<chrono::seconds> (time_ns).count (),
			chrono::duration_cast<chrono::milliseconds> (time_ns).count (),
			(time_ns % 1ms).count ()
		);
		line_writer (s);
	};

	// Do not change the sequence numbers. If a measurement is removed, its sequence number must not be reused.
	// The sequence numbers are used by performance measuring utilities to find the figures.
	log_time ("[2/5] Assembly load"sv, total_assembly_load_time);
	log_time ("[2/6] Java to Managed lookup"sv, total_java_to_managed_time);
	log_time ("[2/7] Managed to Java lookup"sv, total_managed_to_java_time);
	log_time ("[2/8] Assembly decompression"sv, total_assembly_decompression_time);
	log_time ("[2/9] Event timing overhead, per call"sv, static_cast<uint64_t>((start_end_event_time.end - start_end_event_time.start).count ()));
	log_time ("[2/10] clock_gettime overhead, per call"sv, static_cast<uint64_t>((get_time_overhead.end - get_time_overhead.start).count ()));
	log_time ("[2/11] Timing infra init overhead, once"sv, static_cast<uint64_t>((init_time.end - init_time.start).count ()));
}

void FastTiming::dump_to_logcat (size_t entries) noexcept
{
	log_write (LOG_TIMING, LogLevel::Info, "[2/2] Performance measurement results"sv);
	if (no_events_logged (entries)) {
		return;
	}

	auto line_writer = [](std::string_view const& msg) {
		log_write (LOG_TIMING, LogLevel::Info, msg);
	};
	dump<line_writer> (entries, true /* indent */);
}

void FastTiming::dump_to_file (size_t entries) noexcept
{
	if (no_events_logged (entries)) {
		return;
	}
	dynamic_local_string<SENSIBLE_PATH_MAX> timing_log_path;
	timing_log_path.assign (AndroidSystem::get_primary_override_dir ());
	timing_log_path.append (output_file_name == nullptr ? default_timing_file_name : *output_file_name);
	Util::create_directory (AndroidSystem::get_primary_override_dir (), 0755);

	// TODO: implement
}

void FastTiming::dump () noexcept
{
	if (immediate_logging) {
		return;
	}

	StartupAwareLock lock { event_vector_realloc_mutex };
	size_t entries = next_event_index.load ();
	if (log_to_file) {
		dump_to_file (entries);
	} else {
		dump_to_logcat (entries);
	}
}

#include <chrono>

#include <runtime-base/startup-aware-lock.hh>
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

	if (immediate_logging) {
		return;
	}

	log_write (
		LOG_TIMING,
		LogLevel::Info,
		"[2/1] To get timing results, send the mono.android.app.DUMP_TIMING_DATA intent to the application"sv
	);
}

void FastTiming::dump () noexcept
{
	if (immediate_logging) {
		return;
	}

	StartupAwareLock lock { event_vector_realloc_mutex };
	size_t entries = next_event_index.load ();

	log_write (LOG_TIMING, LogLevel::Info, "[2/2] Performance measurement results"sv);
	if (entries == 0) {
		log_write (LOG_TIMING, LogLevel::Info, "[2/3] No events logged"sv);
		return;
	}

	dynamic_local_string<Constants::MAX_LOGCAT_MESSAGE_LENGTH, char> message;

	// Values are in nanoseconds
	uint64_t total_assembly_load_time = 0u;
	uint64_t total_java_to_managed_time = 0u;
	uint64_t total_managed_to_java_time = 0u;
	uint64_t total_ns;

	format_and_log (init_time, message, total_ns, true /* indent */);
	for (TimingEvent const& event : events) {
		format_and_log (event, message, total_ns, true /* indent */);

		switch (event.kind) {
			case TimingEventKind::AssemblyLoad:
				total_assembly_load_time += total_ns;
				break;

			case TimingEventKind::JavaToManaged:
				total_java_to_managed_time += total_ns;
				break;

			case TimingEventKind::ManagedToJava:
				total_managed_to_java_time += total_ns;
				break;

			default:
				// Ignore other kinds
				break;
		}
	}

	log_write (LOG_TIMING, LogLevel::Info, "[2/4] Accumulated performance results"sv);

	auto log_time = [] (std::string_view const& msg, size_t ns)
	{
		chrono::nanoseconds time_ns (ns);
		log_info_nocheck (
			LOG_TIMING,
			"  {}: {}:{}::{}",
			msg,
			chrono::duration_cast<chrono::seconds> (time_ns),
			chrono::duration_cast<chrono::milliseconds> (time_ns),
			time_ns % 1ms
		);
	};

	log_time ("[2/5] Assembly load"sv, total_assembly_load_time);
	log_time ("[2/6] Java to Managed lookup"sv, total_java_to_managed_time);
	log_time ("[2/7] Managed to Java lookup"sv, total_managed_to_java_time);
}

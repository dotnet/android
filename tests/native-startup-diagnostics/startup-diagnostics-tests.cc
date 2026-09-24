#include "startup-diagnostics-internal.hh"
#include "startup-diagnostics.hh"

#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

using namespace xamarin::android::startup;

static void check (bool success, const char* message)
{
	if (success)
		return;
	std::fprintf (stderr, "%s\n", message);
	std::exit (1);
}

static std::atomic<unsigned> identity_reads {0};
static std::atomic<unsigned> clock_reads {0};
static std::atomic<int> sink_result {1};
static std::atomic<uint64_t> thread_id {42};
static bool partial_identity = false;
static bool partial_clock = false;
static bool track_order = false;
static std::string callback_order;
static std::mutex output_lock;
static std::vector<std::string> records;

static Identity read_identity () noexcept
{
	if (track_order)
		callback_order += "identity;";
	identity_reads.fetch_add (1);
	errno = ENOENT;
	Identity identity;
	identity.pid = 42;
	identity.uid = 10151;
	if (!partial_identity) {
		identity.start_ticks = UINT64_MAX;
		std::memcpy (identity.boot_id.data (), "01234567-89ab-cdef-0123-456789abcdef", 37);
	}
	return identity;
}

static uint64_t read_tid () noexcept
{
	errno = EINVAL;
	return thread_id.load ();
}

static ClockSample read_clock () noexcept
{
	if (track_order)
		callback_order += "clock;";
	clock_reads.fetch_add (1);
	errno = ERANGE;
	if (partial_clock)
		return {UINT64_MAX, {}, 1};
	return {100, UINT64_MAX, 101};
}

static int write_record (const char* record) noexcept
{
	std::lock_guard<std::mutex> lock {output_lock};
	records.emplace_back (record);
	errno = EIO;
	return sink_result.load ();
}

static const Platform platform {read_identity, read_tid, read_clock, write_record};

int main (int argc, char** argv)
{
	const char* token = "0123456789abcdef0123456789abcdef";
	const char* values[] = {"UNRELATED", "not emitted", capture_key.data (), token};
	check (find_capture_id (values, 4) == token, "compiled gate");
	check (!find_capture_id (values, 3), "odd compiled array rejected");
	check (!find_capture_id (nullptr, 0), "absent gate");
	const char* duplicates[] = {capture_key.data (), token, capture_key.data (), token};
	check (!find_capture_id (duplicates, 4), "duplicate gate rejected");
	const char* missing[] = {capture_key.data (), nullptr};
	check (!find_capture_id (missing, 2), "null gate rejected");
	for (const char* bad : {"", "0123456789ABCDEF0123456789ABCDEF", "0123456789abcdef0123456789abcdef0",
	                       "../0123456789abcdef0123456789abcd", "0123456789abcdef0123456789abcde\n"}) {
		const char* invalid[] = {capture_key.data (), bad};
		check (!find_capture_id (invalid, 2), "invalid gate rejected");
	}
	check (valid_build_id ("android.d549-readiness_1"), "valid build marker");
	check (!valid_build_id ("-invalid") && !valid_build_id ("unsafe\"") && !valid_build_id (std::string (65, 'a')),
	       "unsafe build marker rejected");
	check (!valid_build_id ("android-d549-diag1\n") && !valid_build_id ("android-d549-diag1\r"), "terminal build marker controls rejected");
	check (parse_unsigned ("18446744073709551615") == UINT64_MAX, "uint64 maximum");
	for (const char* bad : {"", "-1", "+1", " 1", "1x", "18446744073709551616"})
		check (!parse_unsigned (bad), "invalid uint64 rejected");

	std::string stat = "42 (application ) name) S";
	for (int field = 4; field < 22; ++field)
		stat += " 0";
	stat += " 123456 100 200\n";
	check (parse_start_ticks (stat, 42) == 123456, "comm parentheses and field 22");
	check (!parse_start_ticks (stat, 43), "pid mismatch rejected");
	check (!parse_start_ticks ("42 (truncated) S 1 2", 42), "truncated stat rejected");
	check (!parse_start_ticks ("42 missing parentheses", 42), "malformed stat rejected");
	check (valid_boot_id ("01234567-89ab-cdef-0123-456789abcdef"), "boot uuid");
	check (!valid_boot_id ("01234567-89ab-cdef-0123-456789abcde\n"), "unsafe boot uuid");
	check (nanoseconds (1, 2) == 1000000002, "timespec conversion");
	check (!nanoseconds (-1, 0) && !nanoseconds (0, -1) && !nanoseconds (0, 1000000000) &&
	       !nanoseconds (INT64_MAX, 0), "invalid timespec rejected");
	errno = EDOM;
	{
		PreserveErrno guard;
		errno = ERANGE;
	}
	check (errno == EDOM, "ambient errno preserved");

	Recorder disabled {platform};
	disabled.initialize (nullptr, 0, "android-d549-diag1");
	disabled.health ();
	disabled.boundary (Event::JniInit, Phase::Begin);
	check (identity_reads == 0 && clock_reads == 0 && records.empty () && disabled.counters ().attempted == 0,
	       "default off has no identity clock formatting or sink work");
	Recorder no_marker {platform};
	no_marker.initialize (values, 4, "");
	no_marker.health ();
	check (identity_reads == 0 && records.empty (), "absent marker disables");
	Recorder invalid_gate {platform};
	invalid_gate.initialize (duplicates, 4, "android-d549-diag1");
	check (identity_reads == 0 && records.empty (), "invalid gate has no effects");

	Recorder fixture {platform};
	track_order = true;
	fixture.initialize (values, 4, "android-d549-diag1");
	track_order = false;
	check (callback_order == "clock;identity;", "initial clock sampled before identity I/O and not resampled");
	fixture.boundary (Event::JniInit, Phase::Begin);
	fixture.boundary (Event::RuntimeOptions, Phase::Begin);
	fixture.config (ConfigResult::Absent);
	fixture.config (ConfigResult::Invalid);
	fixture.config (ConfigResult::Parsed);
	fixture.expiry (INT64_MIN, INT64_MAX, ExpiryDecision::Enabled);
	fixture.expiry (2, 1, ExpiryDecision::Expired);
	fixture.expiry (0, 0, ExpiryDecision::Disabled);
	fixture.boundary (Event::RuntimeOptions, Phase::End);
	fixture.boundary (Event::DomainInit, Phase::Begin);
	fixture.runtime_config (false, Phase::Instant);
	fixture.runtime_config (true, Phase::Begin);
	fixture.runtime_config (true, Phase::End);
	fixture.boundary (Event::VmInit, Phase::Begin);
	fixture.boundary (Event::VmInit, Phase::End, Outcome::Null);
	fixture.boundary (Event::DomainInit, Phase::End, Outcome::Returned);
	fixture.boundary (Event::JniInit, Phase::End);
	fixture.health ();
	check (errno == EDOM, "all successful observation paths preserve ambient errno");
	check (records.size () == 19 && fixture.counters ().write_accepted == 19, "all event shapes emitted");
	check (records.front ().find ("\"seq\":\"1\"") != std::string::npos &&
	       records.front ().find ("\"writeAccepted\":\"0\"") != std::string::npos, "start snapshot before own sink outcome");
	for (const auto& record : records) {
		check (record.size () <= Recorder::max_record_bytes, "bounded whole record");
		for (unsigned char c : record)
			check (c < 128 && c >= 32, "ASCII single message");
		check (record.find ("not emitted") == std::string::npos && record.find ("UNRELATED") == std::string::npos,
		       "no environment content leaked");
	}
	if (argc == 2 && std::strcmp (argv [1], "--jsonl") == 0) {
		for (const auto& record : records)
			std::puts (record.c_str ());
		return 0;
	}
	records.clear ();
	Recorder failures {platform};
	sink_result = -5;
	failures.initialize (values, 4, "android-d549-diag1");
	sink_result = 0;
	failures.health ();
	sink_result = 1;
	failures.boundary (Event::VmInit, Phase::End);
	thread_id = 0;
	failures.health ();
	thread_id = 42;
	failures.health ();
	auto counts = failures.counters ();
	check (counts.attempted == 5 && counts.write_failed == 2 && counts.format_dropped == 2 && counts.write_accepted == 1,
	       "failure slots not refunded; rejected invalid identity and event");
	check (records.size () == 3 && records.back ().find ("\"writeFailed\":\"2\"") != std::string::npos &&
	       records.back ().find ("\"formatDropped\":\"2\"") != std::string::npos, "explicit sink and format loss health");
	check (errno == EDOM, "failure observations preserve ambient errno");

	records.clear ();
	partial_identity = true;
	partial_clock = true;
	Recorder partial {platform};
	partial.initialize (values, 4, "android-d549-diag1");
	check (records [0].find ("\"identity_status\":\"partial\"") != std::string::npos &&
	       records [0].find ("\"clock_status\":\"partial\"") != std::string::npos &&
	       records [0].find ("\"boot_id\":null") != std::string::npos &&
	       records [0].find ("\"real_ns\":null") != std::string::npos, "unavailable evidence explicit");
	partial_identity = false;
	partial_clock = false;

	records.clear ();
	Recorder concurrent {platform};
	std::vector<std::thread> threads;
	for (int i = 0; i < 8; ++i)
		threads.emplace_back ([&] { concurrent.initialize (values, 4, "android-d549-diag1"); });
	for (auto& thread : threads)
		thread.join ();
	check (records.size () == 1, "one initialization acknowledgement");
	threads.clear ();
	for (int i = 0; i < 8; ++i) {
		threads.emplace_back ([&] {
			errno = EDOM;
			for (int n = 0; n < 128; ++n)
				concurrent.boundary (Event::JniInit, Phase::Begin);
			check (errno == EDOM, "per-thread errno preserved");
		});
	}
	for (auto& thread : threads)
		thread.join ();
	counts = concurrent.counters ();
	check (counts.attempted == 1025 && counts.cap_dropped == 769 && counts.write_accepted == 257 &&
	       counts.write_failed == 0 && counts.format_dropped == 0 && records.size () == 257, "finite concurrent cap and exact accounting");
	unsigned cap_health_count = 0;
	std::vector<bool> seen (258, false);
	for (const auto& record : records) {
		auto start = record.find ("\"seq\":\"");
		check (start != std::string::npos, "sequence exists");
		start += 7;
		auto end = record.find ('"', start);
		auto seq = parse_unsigned (std::string_view {record}.substr (start, end - start));
		check (seq && *seq >= 1 && *seq <= 257 && !seen [static_cast<size_t> (*seq)], "unique bounded sequence");
		seen [static_cast<size_t> (*seq)] = true;
		if (record.find ("\"reason\":\"cap\"") != std::string::npos) {
			++cap_health_count;
			check (*seq == 257, "cap health uses substituted ordinal");
		}
	}
	check (cap_health_count == 1, "sole reserved cap health");
	std::puts ("startup diagnostics tests passed");
}

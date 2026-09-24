#include "startup-diagnostics.hh"
#include "startup-diagnostics-internal.hh"

#include <cinttypes>
#include <cstdio>
#include <cstring>

using namespace xamarin::android::startup;

namespace {
	// Existing logging formatters accept arbitrary app text. This bounded writer accepts only
	// internal literals and validated numeric/identity fields and never emits truncated JSON.
	class Json final
	{
		char* buffer;
		size_t capacity;
		size_t used = 0;
		bool valid = true;
	public:
		Json (char* buffer, size_t capacity) noexcept : buffer {buffer}, capacity {capacity} {}
		void append (const char* text) noexcept
		{
			if (!valid)
				return;
			size_t length = std::strlen (text);
			if (length >= capacity - used) {
				valid = false;
				return;
			}
			std::memcpy (buffer + used, text, length + 1);
			used += length;
		}
		template<typename... Args> void append (const char* format, Args... args) noexcept
		{
			if (!valid)
				return;
			int count = std::snprintf (buffer + used, capacity - used, format, args...);
			if (count < 0 || static_cast<size_t> (count) >= capacity - used) {
				valid = false;
				return;
			}
			used += static_cast<size_t> (count);
		}
		void number (std::optional<uint64_t> value) noexcept
		{
			if (value)
				append ("\"%" PRIu64 "\"", *value);
			else
				append ("null");
		}
		bool good () const noexcept { return valid; }
	};

	const char* event_name (Event event) noexcept
	{
		switch (event) {
			case Event::CaptureStart: return "capture_start";
			case Event::CaptureHealth: return "capture_health";
			case Event::JniInit: return "jni_init";
			case Event::ConfigParse: return "config_parse";
			case Event::ExpiryDecision: return "expiry_decision";
			case Event::RuntimeOptions: return "runtime_options";
			case Event::RuntimeConfig: return "runtime_config";
			case Event::DomainInit: return "domain_init";
			case Event::VmInit: return "vm_init";
		}
		return nullptr;
	}

	const char* phase_name (Phase phase) noexcept
	{
		switch (phase) {
			case Phase::Begin: return "begin";
			case Phase::End: return "end";
			case Phase::Instant: return "instant";
		}
		return nullptr;
	}
}

void Recorder::initialize (const char* const* environment, size_t count, std::string_view marker) noexcept
{
	PreserveErrno guard;
	unsigned expected = 0;
	if (!state.compare_exchange_strong (expected, 1))
		return;
	auto token = find_capture_id (environment, count);
	if (!token || !valid_build_id (marker)) {
		state.store (3, std::memory_order_release);
		return;
	}
	ClockSample initial_clock = platform.clock ();
	std::memcpy (capture_id.data (), token->data (), token->size ());
	std::memcpy (build_id.data (), marker.data (), marker.size ());
	identity = platform.identity ();
	// Publish only after the initial acknowledgement, so capture_start owns ordinal 1.
	attempted.store (1);
	emit (Event::CaptureStart, Phase::Instant, {}, 1, false, &initial_clock);
	state.store (2, std::memory_order_release);
}

Counters Recorder::counters () const noexcept
{
	return {attempted.load (), write_accepted.load (), write_failed.load (), format_dropped.load (), cap_dropped.load ()};
}

void Recorder::record (Event event, Phase phase, const Data& data) noexcept
{
	if (state.load (std::memory_order_acquire) != 2)
		return;
	PreserveErrno guard;
	uint64_t seq = attempted.fetch_add (1) + 1;
	if (seq > max_ordinary_attempts) {
		cap_dropped.fetch_add (1);
		if (seq == max_ordinary_attempts + 1)
			emit (Event::CaptureHealth, Phase::Instant, {}, seq, true);
		return;
	}
	emit (event, phase, data, seq, false);
}

void Recorder::boundary (Event event, Phase phase, Outcome outcome) noexcept
{
	Data data;
	data.outcome = outcome;
	record (event, phase, data);
}

void Recorder::config (ConfigResult result) noexcept
{
	Data data;
	data.config = result;
	record (Event::ConfigParse, Phase::Instant, data);
}

void Recorder::expiry (int64_t now, int64_t deadline, ExpiryDecision decision) noexcept
{
	Data data;
	data.now = now;
	data.deadline = deadline;
	data.decision = decision;
	record (Event::ExpiryDecision, Phase::Instant, data);
}

void Recorder::runtime_config (bool present, Phase phase) noexcept
{
	Data data;
	data.present = present;
	record (Event::RuntimeConfig, phase, data);
}

void Recorder::health () noexcept
{
	record (Event::CaptureHealth, Phase::Instant, {});
}

void Recorder::emit (Event event, Phase phase, const Data& data, uint64_t seq, bool cap, const ClockSample* initial_clock) noexcept
{
	const char* name = event_name (event);
	const char* phase_value = phase_name (phase);
	uint64_t tid = platform.thread_id ();
	if (name == nullptr || phase_value == nullptr || identity.pid == 0 || identity.pid > INT32_MAX ||
	    tid == 0 || tid > INT32_MAX || identity.uid > UINT32_MAX) {
		format_dropped.fetch_add (1);
		return;
	}
	ClockSample clock = initial_clock == nullptr ? platform.clock () : *initial_clock;
	bool boot_valid = identity.boot_id [36] == '\0' && valid_boot_id ({identity.boot_id.data (), 36});
	bool identity_ok = identity.start_ticks && *identity.start_ticks > 0 && boot_valid;
	bool clock_ok = clock.monotonic_before && clock.realtime && clock.monotonic_after &&
	                *clock.monotonic_before <= *clock.monotonic_after;
	char buffer [max_record_bytes + 1];
	Json json {buffer, sizeof (buffer)};
	json.append ("{\"schema\":1,\"component\":\"android\",\"build_id\":\"%s\",\"capture_id\":\"%s\","
	             "\"pid\":%" PRIu64 ",\"tid\":%" PRIu64 ",\"uid\":%" PRIu64 ",\"process_start_ticks\":",
	             build_id.data (), capture_id.data (), identity.pid, tid, identity.uid);
	json.number (identity.start_ticks && *identity.start_ticks > 0 ? identity.start_ticks : std::nullopt);
	json.append (",\"boot_id\":");
	if (boot_valid)
		json.append ("\"%s\"", identity.boot_id.data ());
	else
		json.append ("null");
	json.append (",\"identity_status\":\"%s\",\"seq\":\"%" PRIu64 "\",\"mono_before_ns\":", identity_ok ? "ok" : "partial", seq);
	json.number (clock.monotonic_before);
	json.append (",\"real_ns\":");
	json.number (clock.realtime);
	json.append (",\"mono_after_ns\":");
	json.number (clock.monotonic_after);
	json.append (",\"clock_status\":\"%s\",\"event\":\"%s\",\"phase\":\"%s\",\"data\":{", clock_ok ? "ok" : "partial", name, phase_value);

	bool valid = true;
	switch (event) {
		case Event::CaptureStart:
		case Event::CaptureHealth: {
			Counters snapshot = counters ();
			valid = phase == Phase::Instant;
			json.append ("\"reason\":\"%s\",\"attempted\":\"%" PRIu64 "\",\"writeAccepted\":\"%" PRIu64
			             "\",\"writeFailed\":\"%" PRIu64 "\",\"formatDropped\":\"%" PRIu64 "\",\"capDropped\":\"%" PRIu64 "\"",
			             cap ? "cap" : event == Event::CaptureStart ? "start" : "phase",
			             snapshot.attempted, snapshot.write_accepted, snapshot.write_failed, snapshot.format_dropped, snapshot.cap_dropped);
			break;
		}
		case Event::JniInit:
		case Event::RuntimeOptions:
			valid = phase != Phase::Instant && data.outcome == Outcome::None;
			break;
		case Event::DomainInit:
		case Event::VmInit:
			valid = (phase == Phase::Begin && data.outcome == Outcome::None) ||
			        (phase == Phase::End && (data.outcome == Outcome::Returned || data.outcome == Outcome::Null));
			json.append ("\"result\":%s", data.outcome == Outcome::None ? "null" : data.outcome == Outcome::Returned ? "\"returned\"" : "\"null\"");
			break;
		case Event::ConfigParse:
			valid = phase == Phase::Instant && (data.config == ConfigResult::Absent || data.config == ConfigResult::Parsed || data.config == ConfigResult::Invalid);
			json.append ("\"result\":\"%s\"", data.config == ConfigResult::Absent ? "absent" : data.config == ConfigResult::Parsed ? "parsed" : "invalid");
			break;
		case Event::ExpiryDecision:
			valid = phase == Phase::Instant && (data.decision == ExpiryDecision::Enabled || data.decision == ExpiryDecision::Expired || data.decision == ExpiryDecision::Disabled);
			json.append ("\"guest_now_s\":\"%" PRId64 "\",\"expiry_s\":\"%" PRId64 "\",\"decision\":\"%s\"",
			             data.now, data.deadline, data.decision == ExpiryDecision::Enabled ? "enabled" : data.decision == ExpiryDecision::Expired ? "expired" : "disabled");
			break;
		case Event::RuntimeConfig:
			valid = data.present ? phase != Phase::Instant : phase == Phase::Instant;
			json.append ("\"present\":%s", data.present ? "true" : "false");
			break;
	}
	json.append ("}}");
	if (!valid || !json.good ()) {
		format_dropped.fetch_add (1);
		return;
	}
	if (platform.write (buffer) > 0)
		write_accepted.fetch_add (1);
	else
		write_failed.fetch_add (1);
}

#if defined(XA_STARTUP_DIAGNOSTICS_BUILD_ID)
namespace {
	Recorder recorder {android_platform};
}
void Diagnostics::initialize (const char* const* environment, size_t count) noexcept { recorder.initialize (environment, count, XA_STARTUP_DIAGNOSTICS_BUILD_ID); }
void Diagnostics::boundary (Event event, Phase phase, Outcome outcome) noexcept { recorder.boundary (event, phase, outcome); }
void Diagnostics::config (ConfigResult result) noexcept { recorder.config (result); }
void Diagnostics::expiry (int64_t now, int64_t deadline, ExpiryDecision decision) noexcept { recorder.expiry (now, deadline, decision); }
void Diagnostics::runtime_config (bool present, Phase phase) noexcept { recorder.runtime_config (present, phase); }
void Diagnostics::health () noexcept { recorder.health (); }
#endif

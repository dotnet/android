#pragma once

#include "startup-diagnostics-platform.hh"

#include <atomic>
#include <cstddef>
#include <string_view>

namespace xamarin::android::startup {
	enum class Event { CaptureStart, CaptureHealth, JniInit, ConfigParse, ExpiryDecision, RuntimeOptions, RuntimeConfig, DomainInit, VmInit };
	enum class Phase { Begin, End, Instant };
	enum class Outcome { None, Returned, Null };
	enum class ConfigResult { Absent, Parsed, Invalid };
	enum class ExpiryDecision { Enabled, Expired, Disabled };

	struct Counters final
	{
		uint64_t attempted;
		uint64_t write_accepted;
		uint64_t write_failed;
		uint64_t format_dropped;
		uint64_t cap_dropped;
	};

	class Recorder final
	{
	public:
		static constexpr size_t max_record_bytes = 2048;
		static constexpr uint64_t max_ordinary_attempts = 256;

		explicit Recorder (const Platform& platform) noexcept : platform {platform} {}
		void initialize (const char* const* environment, size_t count, std::string_view build_id) noexcept;
		void boundary (Event event, Phase phase, Outcome outcome = Outcome::None) noexcept;
		void config (ConfigResult result) noexcept;
		void expiry (int64_t now, int64_t deadline, ExpiryDecision decision) noexcept;
		void runtime_config (bool present, Phase phase) noexcept;
		void health () noexcept;
		Counters counters () const noexcept;

	private:
		struct Data {
			Outcome outcome = Outcome::None;
			ConfigResult config = ConfigResult::Absent;
			ExpiryDecision decision = ExpiryDecision::Disabled;
			int64_t now = 0;
			int64_t deadline = 0;
			bool present = false;
		};

		void record (Event event, Phase phase, const Data& data) noexcept;
		void emit (Event event, Phase phase, const Data& data, uint64_t seq, bool cap, const ClockSample* initial_clock = nullptr) noexcept;

		const Platform& platform;
		// 0=uninitialized, 1=initializing, 2=enabled, 3=disabled. No waiting on initialization.
		std::atomic<unsigned> state {0};
		std::array<char, 33> capture_id {};
		std::array<char, 65> build_id {};
		Identity identity {};
		std::atomic<uint64_t> attempted {0};
		std::atomic<uint64_t> write_accepted {0};
		std::atomic<uint64_t> write_failed {0};
		std::atomic<uint64_t> format_dropped {0};
		std::atomic<uint64_t> cap_dropped {0};
	};

	// A normal pack with no producer marker compiles all call sites to no-ops.
	class Diagnostics final
	{
	public:
#if defined(XA_STARTUP_DIAGNOSTICS_BUILD_ID)
		static void initialize (const char* const* environment, size_t count) noexcept;
		static void boundary (Event event, Phase phase, Outcome outcome = Outcome::None) noexcept;
		static void config (ConfigResult result) noexcept;
		static void expiry (int64_t now, int64_t deadline, ExpiryDecision decision) noexcept;
		static void runtime_config (bool present, Phase phase) noexcept;
		static void health () noexcept;
#else
		static void initialize (const char* const*, size_t) noexcept {}
		static void boundary (Event, Phase, Outcome = Outcome::None) noexcept {}
		static void config (ConfigResult) noexcept {}
		static void expiry (int64_t, int64_t, ExpiryDecision) noexcept {}
		static void runtime_config (bool, Phase) noexcept {}
		static void health () noexcept {}
#endif
	};
}

#pragma once

#include <array>
#include <cstdint>
#include <optional>

namespace xamarin::android::startup {
	struct Identity final
	{
		uint64_t pid = 0;
		uint64_t uid = 0;
		std::optional<uint64_t> start_ticks;
		std::array<char, 37> boot_id {};
	};

	struct ClockSample final
	{
		std::optional<uint64_t> monotonic_before;
		std::optional<uint64_t> realtime;
		std::optional<uint64_t> monotonic_after;
	};

	// The host tests exercise the same recorder with deterministic OS outcomes, not an Android runtime.
	struct Platform final
	{
		Identity (*identity) () noexcept;
		uint64_t (*thread_id) () noexcept;
		ClockSample (*clock) () noexcept;
		int (*write) (const char*) noexcept;
	};

	extern const Platform android_platform;
}

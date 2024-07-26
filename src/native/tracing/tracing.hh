#pragma once

#include <cstdint>

namespace xamarin::android {
	enum class TracingAutoStartMode
	{
		None,

		// Start tracing as soon as possible at the application startup
		Startup,

		// Start after an initial delay, counting from the application startup
		Delay,

		// At the application startup, prepare for tracing at a later point. This is to avoid
		// unnecessary delays to load the tracing shared library and initialize everything.
		// Tracing itself will need to be started by a p/invoke or an intent.
		JustInit,
	};

	enum class TracingAutoStopMode
	{
		None,

		// Stop tracing after the designated delay, counted from the moment tracing was started
		DelayFromStart,

		// Stop tracing after the designated delay, counting from application startup
		AbsoluteDelay,
	};

	class TracingConstants
	{
	public:
		static inline constexpr size_t DEFAULT_STOP_DELAY_MS = 2000; // 2s
		static inline constexpr size_t DEFAULT_START_DELAY_MS = 0;
	};
}

#pragma once

#include <cstdint>

namespace xamarin::android {
	enum class LogTimingCategories : uint32_t
	{
		Default  = 0,
		Bare     = 1 << 0,
		FastBare = 1 << 1,
	};

	// Keep in sync with LogLevel defined in JNIEnv.cs
	enum class LogLevel : unsigned int
	{
		Unknown = 0x00,
		Default = 0x01,
		Verbose = 0x02,
		Debug   = 0x03,
		Info    = 0x04,
		Warn    = 0x05,
		Error   = 0x06,
		Fatal   = 0x07,
		Silent  = 0x08
	};
}

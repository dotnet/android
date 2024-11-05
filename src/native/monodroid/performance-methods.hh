#pragma once

#include "timing.hh"
#include "xxhash.hh"
#include "robin_map.hh"

namespace xamarin::android::internal {
	struct MethodEventRecord
	{
		static inline constexpr uint32_t JitStateStarted   = 0x01u;
		static inline constexpr uint32_t JitStateCompleted = 0x02u;
		static inline constexpr uint32_t JitStateSuccess   = 0x04u;

		uint32_t state = 0u;
		uint64_t invocation_count = 0u;
		hash_t  method_name_hash = 0u;
		const char* method_name = nullptr;
		timing_period jit_elapsed {};
	};

	using method_event_map_t = tsl::robin_map<hash_t, MethodEventRecord>;
}

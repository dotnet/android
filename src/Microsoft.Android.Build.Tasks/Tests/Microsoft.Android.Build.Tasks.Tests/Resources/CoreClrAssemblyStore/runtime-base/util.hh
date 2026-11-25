#pragma once

#include <shared/helpers.hh>

extern "C" void* assembly_store_probe_malloc (size_t size) noexcept;

namespace xamarin::android {
	class Constants {
	public:
		static constexpr bool is_debug_build = false;
	};
}

#define log_debugf(...) ((void)0)
#define malloc assembly_store_probe_malloc

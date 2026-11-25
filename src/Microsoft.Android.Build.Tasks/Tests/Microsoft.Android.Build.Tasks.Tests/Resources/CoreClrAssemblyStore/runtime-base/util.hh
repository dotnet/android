#pragma once

#include <shared/helpers.hh>

namespace xamarin::android {
	class Constants {
	public:
		static constexpr bool is_debug_build = false;
	};
}

#define log_debugf(...) ((void)0)

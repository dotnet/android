#pragma once

#include <shared/log_types.hh>

namespace xamarin::android {
	class Logger
	{
	public:
		static void init_logging_categories () noexcept;
	private:
		static void set_category (const char *name, const char *arg, size_t arg_length, unsigned int entry) noexcept;
	};
}

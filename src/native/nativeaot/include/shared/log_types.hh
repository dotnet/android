#pragma once

#include <string_view>

#include <shared/log_functions.hh>

namespace xamarin::android {
	[[gnu::always_inline]]
	static inline void log_write (LogCategories category, LogLevel level, std::string_view const& message) noexcept
	{
		log_write (category, level, message.data ());
	}
}

extern unsigned int log_categories;

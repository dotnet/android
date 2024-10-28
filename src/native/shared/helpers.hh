#ifndef __HELPERS_HH
#define __HELPERS_HH

#include <cstdlib>
#include <source_location>

#include <java-interop-util.h>
#include "platform-compat.hh"

namespace xamarin::android
{
	class [[gnu::visibility("hidden")]] Helpers
	{
	public:
		template<typename Ret, typename P1, typename P2>
		force_inline static Ret add_with_overflow_check (P1 a, P2 b, std::source_location sloc = std::source_location::current ()) noexcept
		{
			constexpr bool DoNotLogLocation = false;
			Ret ret;

			if (__builtin_add_overflow (a, b, &ret)) [[unlikely]] {
				// It will leak memory, but it's fine, we're exiting the app anyway
				char *message = nullptr;
				int n = asprintf (&message, "Integer overflow on addition at %s:%u", sloc.file_name (), sloc.line ());
				abort_application (n == -1 ? "Integer overflow on addition" : message, DoNotLogLocation, sloc);
			}

			return ret;
		}

		template<typename Ret, typename P1, typename P2>
		force_inline static Ret multiply_with_overflow_check (P1 a, P2 b, std::source_location sloc = std::source_location::current ()) noexcept
		{
			constexpr bool DoNotLogLocation = false;
			Ret ret;

			if (__builtin_mul_overflow (a, b, &ret)) [[unlikely]] {
				// It will leak memory, but it's fine, we're exiting the app anyway
				char *message = nullptr;
				int n = asprintf (&message, "Integer overflow on multiplication at %s:%u", sloc.file_name (), sloc.line ());
				abort_application (n == -1 ? "Integer overflow on multiplication" : message, DoNotLogLocation, sloc);
			}

			return ret;
		}

		[[noreturn]] static void abort_application (LogCategories category, const char *message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept;

		[[noreturn]] static void abort_application (const char *message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept
		{
			abort_application (LOG_DEFAULT, message, log_location, sloc);
		}
	};
}
#endif // __HELPERS_HH

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
				log_fatal (LOG_DEFAULT, "Integer overflow on addition at %s:%u", sloc.file_name (), sloc.line ());
				abort_application (DoNotLogLocation, sloc);
			}

			return ret;
		}

		template<typename Ret, typename P1, typename P2>
		force_inline static Ret multiply_with_overflow_check (P1 a, P2 b, std::source_location sloc = std::source_location::current ()) noexcept
		{
			constexpr bool DoNotLogLocation = false;
			Ret ret;

			if (__builtin_mul_overflow (a, b, &ret)) [[unlikely]] {
				log_fatal (LOG_DEFAULT, "Integer overflow on multiplication at %s:%u", sloc.file_name (), sloc.line ());
				abort_application (DoNotLogLocation, sloc);
			}

			return ret;
		}

		[[noreturn]] static void abort_application (bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept;
	};
}
#endif // __HELPERS_HH

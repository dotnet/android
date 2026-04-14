#pragma once

#include <cstdlib>
#include <cstdio>
#include <cstdarg>
#include <source_location>
#include <string>
#include <string_view>

#include <java-interop-util.h>

using namespace std::string_view_literals;

namespace xamarin::android
{
	namespace detail {
		template<typename T>
		concept TPointer = requires { std::is_pointer_v<T>; };
	}

	class [[gnu::visibility("hidden")]] Helpers
	{
	public:
		template<typename Ret, typename P1, typename P2>
		[[gnu::always_inline]]
		static auto add_with_overflow_check (P1 a, P2 b, std::source_location sloc = std::source_location::current ()) noexcept -> Ret
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
		[[gnu::always_inline]]
		static auto multiply_with_overflow_check (P1 a, P2 b, std::source_location sloc = std::source_location::current ()) noexcept -> Ret
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

		[[noreturn]]
		static void abort_application (LogCategories category, const char *message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept;

		[[noreturn]]
		static void abort_application (LogCategories category, std::string const& message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept
		{
			abort_application (category, message.c_str (), log_location, sloc);
		}

		[[noreturn]]
		static void abort_application (LogCategories category, std::string_view const& message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept
		{
			abort_application (category, message.data (), log_location, sloc);
		}

		[[noreturn]]
		static void abort_application (const char *message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept
		{
			abort_application (LOG_DEFAULT, message, log_location, sloc);
		}

		[[noreturn]]
		static void abort_application (std::string const& message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept
		{
			abort_application (LOG_DEFAULT, message.c_str (), log_location, sloc);
		}

		[[noreturn]]
		static void abort_application (std::string_view const& message, bool log_location = true, std::source_location sloc = std::source_location::current ()) noexcept
		{
			abort_application (LOG_DEFAULT, message.data (), log_location, sloc);
		}
	};

	template<detail::TPointer TRet = void*, detail::TPointer TPtr> [[gnu::always_inline]]
	static inline constexpr auto pointer_add (TPtr ptr, size_t offset) noexcept -> TRet
	{
		return reinterpret_cast<TRet>(reinterpret_cast<uintptr_t>(ptr) + offset);
	}

	[[gnu::always_inline]]
	static inline constexpr auto optional_string (const char* s, const char *replacement = nullptr) noexcept -> const char*
	{
		if (s != nullptr) [[likely]] {
			return s;
		}

		return replacement == nullptr ? "<null>" : replacement;
	}
}

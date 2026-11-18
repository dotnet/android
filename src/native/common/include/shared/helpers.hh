#pragma once

#include <cstdlib>
#include <cstring>
#include <source_location>
#include <string>
#include <string_view>

#include <shared/log_categories.hh>

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
		static void abort_applicationf (LogCategories category, std::source_location sloc, const char *format, ...) noexcept __attribute__ ((format (__printf__, 3, 4)));

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

	// A thread-safe replacement for `strerror ()`. The message lives in a buffer owned by the object, so
	// `c_str ()` stays valid for as long as the object does. Create it in the expression which uses the message:
	//
	//   log_warnf (LOG_DEFAULT, "Failed: %s", ErrorMessage (errno).c_str ());
	class ErrorMessage final
	{
	public:
		explicit ErrorMessage (int error_number) noexcept
			: message { strerror_r (error_number, buffer, sizeof (buffer)) }
		{}

		ErrorMessage (ErrorMessage const&) = delete;
		auto operator= (ErrorMessage const&) -> ErrorMessage& = delete;

		auto c_str () const noexcept -> const char*
		{
			return message;
		}

	private:
		char buffer [128];
		const char *message;
	};
}

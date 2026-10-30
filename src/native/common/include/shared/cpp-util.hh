#pragma once

#include <array>
#include <cstdarg>
#include <cstdlib>
#include <cstdio>
#include <concepts>
#include <functional>
#include <source_location>
#include <string_view>
#include <type_traits>

#include <semaphore.h>
#include <android/log.h>

#include <shared/helpers.hh>

namespace xamarin::android::detail {
	[[gnu::always_inline, gnu::flatten]]
	static inline const char*
	_format_message (const char *format, ...) noexcept
	{
		va_list ap;
		va_start (ap, format);

		char *message;
		int ret = vasprintf (&message, format, ap);

		va_end (ap);
		return ret == -1 ? "Out of memory" : message;
	}
}

template<std::invocable<> F>
[[gnu::always_inline, gnu::flatten]]
static inline void
abort_unless (bool condition, F&& get_message, std::source_location sloc = std::source_location::current ()) noexcept
{
	static_assert (std::is_same<typename std::invoke_result<F>::type, const char*>::value, "get_message must return 'const char*'");

	if (condition) [[likely]] {
		return;
	}

	xamarin::android::Helpers::abort_application (std::invoke (get_message), true /* log_location */, sloc);
}

[[gnu::always_inline, gnu::flatten]]
static inline void
abort_unless (bool condition, const char *message, std::source_location sloc = std::source_location::current ()) noexcept
{
	if (condition) [[likely]] {
		return;
	}
	xamarin::android::Helpers::abort_application (message, true /* log_location */, sloc);
}

template<typename T>
[[gnu::always_inline, gnu::flatten]]
static inline void
abort_if_invalid_pointer_argument (T *ptr, const char *ptr_name, std::source_location sloc = std::source_location::current (), const char *function_name = __builtin_FUNCTION ()) noexcept
{
	abort_unless (
		ptr != nullptr,
		[&ptr_name, &function_name] {
			// The default argument captures the caller's unqualified name; sloc retains the full signature.
			return xamarin::android::detail::_format_message (
				"%s: parameter '%s' must be a valid pointer",
				function_name,
				ptr_name
			);
		},
		sloc
	);
}

namespace xamarin::android
{
	template<class T>
	concept StringViewPart = std::is_same_v<T, std::string_view>;

	template<size_t TotalLength, StringViewPart ...T>
	consteval auto concat_string_views (T const&... parts)
	{
		std::array<char, TotalLength + 1> ret; // lgtm [cpp/paddingbyteinformationdisclosure] the buffer is filled in the loop below
		ret[TotalLength] = 0;

		size_t i = 0;
		for (std::string_view const& sv : {parts...}) {
			for (const char ch : sv) {
				ret[i] = ch;
				i++;
			}
		}

		return ret;
	}

	consteval size_t calc_size (std::string_view const& sv1) noexcept
	{
		return sv1.size ();
	}

	template<StringViewPart ...T>
	consteval size_t calc_size (std::string_view const& sv1, T const&... other_svs) noexcept
	{
		return sv1.size () + calc_size (other_svs...);
	}

	template <typename TEnum, std::enable_if_t<std::is_enum_v<TEnum>, int> = 0>
	constexpr TEnum operator & (TEnum l, TEnum r) noexcept
	{
		using etype = std::underlying_type_t<TEnum>;
		return static_cast<TEnum>(static_cast<etype>(l) & static_cast<etype>(r));
	}

	template <typename TEnum, std::enable_if_t<std::is_enum_v<TEnum>, int> = 0>
	constexpr TEnum& operator &= (TEnum& l, TEnum r) noexcept
	{
		return l = (l & r);
	}

	template <typename TEnum, std::enable_if_t<std::is_enum_v<TEnum>, int> = 0>
	constexpr TEnum operator | (TEnum l, TEnum r) noexcept
	{
		using etype = std::underlying_type_t<TEnum>;
		return static_cast<TEnum>(static_cast<etype>(l) | static_cast<etype>(r));
	}

	template <typename TEnum, std::enable_if_t<std::is_enum_v<TEnum>, int> = 0>
	constexpr TEnum& operator |= (TEnum& l, TEnum r) noexcept
	{
		return l = (l | r);
	}

	template <typename TEnum, std::enable_if_t<std::is_enum_v<TEnum>, int> = 0>
	constexpr TEnum operator ~ (TEnum r) noexcept
	{
		using etype = std::underlying_type_t<TEnum>;
		return static_cast<TEnum> (~static_cast<etype>(r));
	}

	template <typename TEnum, std::enable_if_t<std::is_enum_v<TEnum>, int> = 0>
	constexpr TEnum operator ^ (TEnum l, TEnum r) noexcept
	{
		using etype = std::underlying_type_t<TEnum>;
		return static_cast<TEnum>(static_cast<etype>(l) ^ static_cast<etype>(r));
	}

	template <typename TEnum, std::enable_if_t<std::is_enum_v<TEnum>, int> = 0>
	constexpr TEnum& operator ^= (TEnum& l, TEnum r) noexcept
	{
		return l = (l ^ r);
	}
}

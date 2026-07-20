#pragma once

#include <array>
#include <cstdarg>
#include <cstdlib>
#include <cstdio>
#include <concepts>
#include <memory>
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

	static inline constexpr size_t find_function_parameter_list (std::string_view signature) noexcept
	{
		using std::operator""sv;

		size_t search_end = signature.find (" ["sv);
		if (search_end == std::string_view::npos) {
			search_end = signature.length ();
		}

		if (search_end == 0) {
			return std::string_view::npos;
		}

		size_t close_pos = signature.rfind (')', search_end - 1);
		if (close_pos == std::string_view::npos) {
			return std::string_view::npos;
		}

		size_t depth = 0;
		for (size_t pos = close_pos + 1; pos > 0;) {
			char ch = signature [--pos];
			if (ch == ')') {
				depth++;
			} else if (ch == '(') {
				if (depth == 0) [[unlikely]] {
					return std::string_view::npos;
				}

				depth--;
				if (depth == 0) {
					return pos;
				}
			}
		}

		return std::string_view::npos;
	}

	static inline constexpr size_t find_function_component_start (std::string_view signature, size_t component_end) noexcept
	{
		size_t nesting_depth = 0;

		for (size_t pos = component_end; pos > 0;) {
			char ch = signature [--pos];
			if (ch == ')' || ch == '>' || ch == ']') {
				nesting_depth++;
			} else if (ch == '(' || ch == '<' || ch == '[') {
				if (nesting_depth > 0) {
					nesting_depth--;
				}
			} else if (ch == ' ' && nesting_depth == 0) {
				return pos + 1;
			}
		}

		return 0;
	}

	static inline constexpr std::string_view get_function_name (const char *signature) noexcept
	{
		using std::operator""sv;

		if (signature == nullptr || *signature == '\0') {
			return "<unknown function>"sv;
		}

		std::string_view sig { signature };
		size_t name_end = find_function_parameter_list (sig);
		if (name_end == std::string_view::npos) {
			name_end = sig.length ();
		}

		size_t operator_pos = sig.rfind ("operator"sv, name_end);
		if (operator_pos != std::string_view::npos) {
			auto is_identifier_character = [](char ch) constexpr {
				return (ch >= 'a' && ch <= 'z') ||
					(ch >= 'A' && ch <= 'Z') ||
					(ch >= '0' && ch <= '9') ||
					ch == '_';
			};

			size_t operator_end = operator_pos + "operator"sv.length ();
			if ((operator_pos > 0 && is_identifier_character (sig [operator_pos - 1])) ||
			    (operator_end < name_end && is_identifier_character (sig [operator_end]))) {
				operator_pos = std::string_view::npos;
			}
		}

		size_t scope_pos = sig.rfind ("::"sv, operator_pos == std::string_view::npos ? name_end : operator_pos);
		bool have_scoped_operator = operator_pos != std::string_view::npos &&
			scope_pos != std::string_view::npos &&
			scope_pos + 2 == operator_pos;

		size_t qualified_name_start = find_function_component_start (sig, have_scoped_operator ? scope_pos : name_end);
		size_t name_start = qualified_name_start;
		if (operator_pos != std::string_view::npos && !have_scoped_operator) {
			name_start = operator_pos;
		} else if (scope_pos != std::string_view::npos && scope_pos >= qualified_name_start) {
			size_t previous_scope_pos = scope_pos == 0 ? std::string_view::npos : sig.rfind ("::"sv, scope_pos - 1);
			if (previous_scope_pos != std::string_view::npos && previous_scope_pos >= qualified_name_start) {
				name_start = previous_scope_pos + 2;
			}
		}

		if (name_start >= name_end || name_start > sig.length ()) [[unlikely]] {
			name_start = 0;
		}

		return sig.substr (name_start, name_end - name_start);
	}

	static_assert (get_function_name ("void ordinary_function()") == std::string_view { "ordinary_function" });
	static_assert (get_function_name ("void example::Widget::method(int)") == std::string_view { "Widget::method" });
	static_assert (get_function_name ("void example::Widget<int>::method()") == std::string_view { "Widget<int>::method" });
	static_assert (get_function_name ("void example::Widget<T>::method(U) [with T = int; U = long int]") == std::string_view { "Widget<T>::method" });
	static_assert (get_function_name ("void example::Widget::method()::<lambda(int)>::operator()(int) const") == std::string_view { "<lambda(int)>::operator()" });
	static_assert (get_function_name ("bool operator==(const Value&, const Value&)") == std::string_view { "operator==" });
	static_assert (get_function_name ("bool example::Value::operator==(const Value&) const") == std::string_view { "Value::operator==" });
	static_assert (get_function_name ("void malformed(") == std::string_view { "malformed(" });
	static_assert (get_function_name (nullptr) == std::string_view { "<unknown function>" });
	static_assert (get_function_name ("") == std::string_view { "<unknown function>" });
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
abort_if_invalid_pointer_argument (T *ptr, const char *ptr_name, std::source_location sloc = std::source_location::current ()) noexcept
{
	abort_unless (
		ptr != nullptr,
		[&ptr_name, &sloc] {
			std::string_view function_name = xamarin::android::detail::get_function_name (sloc.function_name ());
			return xamarin::android::detail::_format_message (
				"%.*s: parameter '%s' must be a valid pointer",
				static_cast<int>(function_name.length ()),
				function_name.data (),
				ptr_name
			);
		},
		sloc
	);
}

[[gnu::always_inline, gnu::flatten]]
static inline void
abort_if_negative_integer_argument (int arg, const char *arg_name, std::source_location sloc = std::source_location::current ()) noexcept
{
	abort_unless (
		arg > 0,
		[&arg_name, &sloc] {
			std::string_view function_name = xamarin::android::detail::get_function_name (sloc.function_name ());
			return xamarin::android::detail::_format_message (
				"%.*s: parameter '%s' must be a positive integer",
				static_cast<int>(function_name.length ()),
				function_name.data (),
				arg_name
			);
		},
		sloc
	);
}

namespace xamarin::android
{
	template <typename T>
	struct CDeleter final
	{
		using UnderlyingType = std::remove_cv_t<T>;

		void operator() (T* p)
		{
			UnderlyingType *ptr;

			if constexpr (std::is_const_v<T>) {
				ptr = const_cast<std::remove_const_t<T>*> (p);
			} else {
				ptr = p;
			}

			std::free (reinterpret_cast<void*>(ptr));
		}
	};

	template <typename T>
	using c_unique_ptr = std::unique_ptr<T, CDeleter<T>>;

	template<size_t Size>
	using char_array = std::array<char, Size>;

	template<size_t ...Length>
	constexpr auto concat_const (const char (&...parts)[Length])
	{
		// `parts` being constant string arrays, Length for each of them includes the trailing NUL byte, thus the
		// `sizeof... (Length)` part which subtracts the number of template parameters - the amount of NUL bytes so that
		// we don't waste space.
		constexpr size_t total_length = (... + Length) - sizeof... (Length);
		char_array<total_length + 1> ret; // lgtm [cpp/paddingbyteinformationdisclosure] the buffer is filled in the loop below
		ret[total_length] = 0;

		size_t i = 0uz;
		for (char const* from : {parts...}) {
			for (; *from != '\0'; i++) {
				ret[i] = *from++;
			}
		}

		return ret;
	};

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

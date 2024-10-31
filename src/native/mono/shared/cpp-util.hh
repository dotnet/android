#ifndef __CPP_UTIL_HH
#define __CPP_UTIL_HH

#include <array>
#include <cstdarg>
#include <cstdlib>
#include <concepts>
#include <memory>
#include <ranges>
#include <source_location>
#include <string>
#include <string_view>
#include <type_traits>
#include <vector>

#include <semaphore.h>
#include <android/log.h>

#include <shared/helpers.hh>


static inline void
do_abort_unless (const char* fmt, ...)
{
	va_list ap;

		char *message;
		int ret = vasprintf (&message, format, ap);

	xamarin::android::Helpers::abort_application (n == -1 ? "Unable to allocate memory for abort message" : message);
}

// #define abort_unless(_condition_, _fmt_, ...) \
// 	if (!(_condition_)) [[unlikely]] { \
// 		do_abort_unless ("%s:%d (%s): " _fmt_, __FILE__, __LINE__, __FUNCTION__, ## __VA_ARGS__); \
// 	}

template<std::invocable<> F>
[[gnu::always_inline, gnu::flatten]]
static inline void
abort_unless (bool condition, F&& get_message, std::source_location sloc = std::source_location::current ()) noexcept
{
	if (condition) [[likely]] {
		return;
	}

//	static_assert (std::is_same<std::invoke_result<F>, char*>::value, "get_message must return 'char*'");
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

// #define abort_if_invalid_pointer_argument(_ptr_) abort_unless ((_ptr_) != nullptr, "Parameter '%s' must be a valid pointer", #_ptr_)
// #define abort_if_negative_integer_argument(_arg_) abort_unless ((_arg_) > 0, "Parameter '%s' must be larger than 0", #_arg_)

template<typename T>
[[gnu::always_inline, gnu::flatten]]
static inline void
abort_if_invalid_pointer_argument (T *ptr, const char *ptr_name, std::source_location sloc = std::source_location::current ()) noexcept
{
	abort_unless (ptr != nullptr, "Parameter '%s' must be a valid pointer", sloc);
}

[[gnu::always_inline, gnu::flatten]]
static inline void
abort_if_negative_integer_argument (int arg, const char *arg_name, std::source_location sloc = std::source_location::current ()) noexcept
{
	abort_unless (arg > 0, "Parameter '%s' must be a valid pointer", sloc);
}

// Helper to use in "printf debugging". Normally not used in code anywhere. No code should be shipped with any
// of the calls present.
force_inline inline void pd_log_location (std::source_location sloc = std::source_location::current ()) noexcept
{
	log_info_nocheck (LOG_DEFAULT, "loc: {}:{} ('{}')", sloc.file_name (), sloc.line (), sloc.function_name ());
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
#endif // !def __CPP_UTIL_HH

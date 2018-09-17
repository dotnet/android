// This is a -*- C++ -*- header
#ifndef __CPP_COMPAT_H
#define __CPP_COMPAT_H

// Since Android doesn't currently have any standard C++ library
// and we don't want to use any implementation of it shipped in
// source form with the NDK (for space reasons), this header will
// contain implementations of certain C++ standard functions, classes
// etc we want to use despite lack of the STL.
//
// When/if we have any STL implementation available on standard Android
// we can remove this file.
namespace std
{
	template <typename T> struct remove_reference      { using type = T; };
	template <typename T> struct remove_reference<T&>  { using type = T; };
	template <typename T> struct remove_reference<T&&> { using type = T; };

	template<typename T> typename remove_reference<T>::type&& move (T&& arg) noexcept
	{
		return static_cast<typename remove_reference<decltype(arg)>::type&&>(arg);
	}
}
#endif

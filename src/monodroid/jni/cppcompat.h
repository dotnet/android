// This is a -*- C++ -*- header
#ifndef __CPP_COMPAT_H
#define __CPP_COMPAT_H

#include <pthread.h>

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

	template<typename TMutex>
	class lock_guard
	{
	public:
		using mutex_type = TMutex;

	public:
		lock_guard (const lock_guard&) = delete;

		explicit lock_guard (mutex_type& _mutex)
			: _mutex (_mutex)
		{
			_mutex.lock ();
		}

		~lock_guard ()
		{
			_mutex.unlock ();
		}

		lock_guard& operator= (const lock_guard&) = delete;

	private:
		mutex_type &_mutex;
	};

	class mutex
	{
	public:
		mutex () noexcept = default;
		~mutex () noexcept = default;

		void lock () noexcept
		{
			pthread_mutex_lock (&_pmutex);
		}

		void unlock () noexcept
		{
			pthread_mutex_unlock (&_pmutex);
		}
	private:
		pthread_mutex_t _pmutex = PTHREAD_MUTEX_INITIALIZER;
	};
}
#endif

// This is a -*- C++ -*- header
#ifndef __CPP_COMPAT_H
#define __CPP_COMPAT_H

#include <pthread.h>

#undef HAVE_WORKING_MUTEX

// On desktop builds we can include the actual C++ standard library files which declare type traits
// as well as the `lock_guard` and `mutex` classes. However, some versions of MinGW, even though
// they have the required files, do not declare `mutex` because the `gthreads` feature is not
// enabled. Thus the complicated `#if` below.
#if !defined (ANDROID) && (!defined (WINDOWS) || (defined (WINDOWS) && defined (_GLIBCXX_HAS_GTHREADS)))
#define HAVE_WORKING_MUTEX 1
#endif

// We can't use <mutex> on Android because it requires linking libc++ into the rutime, see below.
#if !defined (ANDROID)
#include <type_traits>
#include <mutex> // Also declares `lock_guard` even if it doesn't declare `mutex`
#endif

// Android NDK currently provides a build of libc++ which we cannot link into Xamarin.Android runtime because it would
// expose libc++ symbols which would conflict with a version of libc++ potentially included in a mixed
// native/Xamarin.Android application.
//
// Until we can figure out a way to take full advantage of the STL, this header will
// contain implementations of certain C++ standard functions, classes
// etc we want to use despite lack of the STL.
//
// When/if we have any STL implementation available on standard Android
// we can remove this file.
namespace std
{
#if defined (ANDROID)
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
#endif // !def ANDROID

#if !defined (HAVE_WORKING_MUTEX)
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
#endif // !def HAVE_WORKING_MUTEX
}

#endif

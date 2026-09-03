#pragma once

#include <pthread.h>

namespace xamarin::android
{
	// A minimal replacement for `std::mutex`, which we cannot use because it makes the runtime
	// depend on libc++. See https://github.com/dotnet/android/issues/12533
	//
	// `PTHREAD_MUTEX_INITIALIZER` lets instances be constant-initialized, so static members of
	// this type need neither dynamic initialization nor a thread-safe initialization guard.
	class Mutex final
	{
	public:
		constexpr Mutex () noexcept = default;

		Mutex (Mutex const&) = delete;
		Mutex (Mutex&&) = delete;

		auto operator= (Mutex const&) -> Mutex& = delete;
		auto operator= (Mutex&&) -> Mutex& = delete;

		void lock () noexcept
		{
			pthread_mutex_lock (&mutex);
		}

		void unlock () noexcept
		{
			pthread_mutex_unlock (&mutex);
		}

	private:
		pthread_mutex_t mutex = PTHREAD_MUTEX_INITIALIZER;
	};

	// A minimal replacement for `std::lock_guard`
	class MutexGuard final
	{
	public:
		explicit MutexGuard (Mutex &mutex) noexcept
			: mutex (mutex)
		{
			mutex.lock ();
		}

		~MutexGuard () noexcept
		{
			mutex.unlock ();
		}

		MutexGuard (MutexGuard const&) = delete;
		MutexGuard (MutexGuard&&) = delete;

		auto operator= (MutexGuard const&) -> MutexGuard& = delete;
		auto operator= (MutexGuard&&) -> MutexGuard& = delete;

	private:
		Mutex &mutex;
	};
}

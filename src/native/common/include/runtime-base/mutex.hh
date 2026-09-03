#pragma once

#include <pthread.h>

namespace xamarin::android
{
	// Scope-based locking without `std::lock_guard`, which would make the runtime depend on libc++.
	// See https://github.com/dotnet/android/issues/12533
	class MutexGuard final
	{
	public:
		explicit MutexGuard (pthread_mutex_t &mutex) noexcept
			: mutex (mutex)
		{
			pthread_mutex_lock (&mutex);
		}

		~MutexGuard () noexcept
		{
			pthread_mutex_unlock (&mutex);
		}

		MutexGuard (MutexGuard const&) = delete;
		MutexGuard (MutexGuard&&) = delete;

		auto operator= (MutexGuard const&) -> MutexGuard& = delete;
		auto operator= (MutexGuard&&) -> MutexGuard& = delete;

	private:
		pthread_mutex_t &mutex;
	};
}

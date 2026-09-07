#pragma once

#include <pthread.h>

namespace xamarin::android
{
	// Scope-based locking without `std::lock_guard`, which would make the runtime depend on libc++.
	// See https://github.com/dotnet/android/issues/12533
	class lock_guard final
	{
	public:
		explicit lock_guard (pthread_mutex_t &mutex) noexcept
			: mutex (mutex)
		{
			pthread_mutex_lock (&mutex);
		}

		~lock_guard () noexcept
		{
			pthread_mutex_unlock (&mutex);
		}

		lock_guard (lock_guard const&) = delete;
		lock_guard (lock_guard&&) = delete;

		auto operator= (lock_guard const&) -> lock_guard& = delete;
		auto operator= (lock_guard&&) -> lock_guard& = delete;

	private:
		pthread_mutex_t &mutex;
	};
}

#pragma once

#include <pthread.h>

namespace xamarin::android
{
	// Scope-based locking without `std::lock_guard`, which would make the runtime depend on libc++.
	// See https://github.com/dotnet/android/issues/12533
	class pthread_mutex_guard final
	{
	public:
		explicit pthread_mutex_guard (pthread_mutex_t &mutex) noexcept
			: mutex (mutex)
		{
			pthread_mutex_lock (&mutex);
		}

		~pthread_mutex_guard () noexcept
		{
			pthread_mutex_unlock (&mutex);
		}

		pthread_mutex_guard (pthread_mutex_guard const&) = delete;
		pthread_mutex_guard (pthread_mutex_guard&&) = delete;

		auto operator= (pthread_mutex_guard const&) -> pthread_mutex_guard& = delete;
		auto operator= (pthread_mutex_guard&&) -> pthread_mutex_guard& = delete;

	private:
		pthread_mutex_t &mutex;
	};
}

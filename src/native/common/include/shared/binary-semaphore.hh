#pragma once

#include <ctime>
#include <pthread.h>

#include <shared/helpers.hh>

namespace xamarin::android {
	// A minimal binary semaphore built directly on top of pthreads.
	//
	// `std::binary_semaphore` would do the same job, but it drags in `std::chrono::steady_clock::now`
	// together with libc++'s atomic wait/notify and timed backoff helpers, all of which make us depend
	// on `libc++`. The pthread primitives used here all live in `libc`.
	//
	// Unlike `std::binary_semaphore`, `try_acquire_for` waits on `CLOCK_MONOTONIC`, so the timeout is
	// unaffected by wall clock adjustments.
	class BinarySemaphore
	{
	public:
		explicit BinarySemaphore () noexcept
		{
			pthread_condattr_t attr;

			// None of these calls can fail for the arguments we pass, but a silently broken semaphore
			// would be very hard to diagnose later on, so make sure we notice.
			if (pthread_condattr_init (&attr) != 0 ||
				pthread_condattr_setclock (&attr, CLOCK_MONOTONIC) != 0 ||
				pthread_cond_init (&cond, &attr) != 0) [[unlikely]] {
				Helpers::abort_application ("Failed to initialize a binary semaphore."sv);
			}

			pthread_condattr_destroy (&attr);
		}

		BinarySemaphore (const BinarySemaphore&) = delete;
		BinarySemaphore (BinarySemaphore&&) = delete;

		~BinarySemaphore () noexcept
		{
			pthread_cond_destroy (&cond);
			pthread_mutex_destroy (&mutex);
		}

		BinarySemaphore& operator= (const BinarySemaphore&) = delete;
		BinarySemaphore& operator= (BinarySemaphore&&) = delete;

		void release () noexcept
		{
			pthread_mutex_lock (&mutex);
			signalled = true;
			pthread_mutex_unlock (&mutex);

			pthread_cond_signal (&cond);
		}

		// Returns `false` if the timeout elapsed before the semaphore was released.
		[[nodiscard]] auto try_acquire_for (unsigned int timeout_ms) noexcept -> bool
		{
			constexpr long NanosecondsPerSecond = 1000000000L;
			constexpr long NanosecondsPerMillisecond = 1000000L;
			constexpr unsigned int MillisecondsPerSecond = 1000u;

			timespec deadline {};
			if (clock_gettime (CLOCK_MONOTONIC, &deadline) != 0) [[unlikely]] {
				// Leaving `deadline` at zero would make `pthread_cond_timedwait` return `ETIMEDOUT`
				// immediately, turning this into a silent spurious timeout.
				Helpers::abort_application ("Failed to read the monotonic clock.");
			}

			deadline.tv_sec += static_cast<time_t>(timeout_ms / MillisecondsPerSecond);
			deadline.tv_nsec += static_cast<long>(timeout_ms % MillisecondsPerSecond) * NanosecondsPerMillisecond;
			if (deadline.tv_nsec >= NanosecondsPerSecond) {
				deadline.tv_sec++;
				deadline.tv_nsec -= NanosecondsPerSecond;
			}

			pthread_mutex_lock (&mutex);

			// The loop takes care of spurious wakeups, `pthread_cond_timedwait` returns `ETIMEDOUT` once
			// the deadline passes.
			int ret = 0;
			while (!signalled && ret == 0) {
				ret = pthread_cond_timedwait (&cond, &mutex, &deadline);
			}

			bool acquired = signalled;
			signalled = false;

			pthread_mutex_unlock (&mutex);

			return acquired;
		}

	private:
		pthread_mutex_t mutex = PTHREAD_MUTEX_INITIALIZER;
		pthread_cond_t  cond {};
		bool            signalled = false;
	};
}

#pragma once

#include <runtime-base/mutex.hh>

#include "monodroid-state.hh"

namespace xamarin::android
{
	class StartupAwareLock final
	{
	public:
		explicit StartupAwareLock (pthread_mutex_t &m)
			: lock (m)
		{
			if (MonodroidState::is_startup_in_progress ()) {
				// During startup we run without threads, do nothing
				return;
			}
			pthread_mutex_lock (&lock);
			owns_lock = true;
		}

		~StartupAwareLock ()
		{
			if (owns_lock) {
				pthread_mutex_unlock (&lock);
			}
		}

		StartupAwareLock (StartupAwareLock const&) = delete;
		StartupAwareLock (StartupAwareLock const&&) = delete;

		StartupAwareLock& operator= (StartupAwareLock const&) = delete;

	private:
		pthread_mutex_t &lock;
		bool owns_lock = false;
	};
}

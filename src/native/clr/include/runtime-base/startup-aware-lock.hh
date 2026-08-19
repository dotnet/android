#pragma once

#include <mutex>

#include "monodroid-state.hh"

namespace xamarin::android
{
	class StartupAwareLock final
	{
	public:
		explicit StartupAwareLock (std::mutex &m)
			: lock (m)
		{
			if (MonodroidState::is_startup_in_progress ()) {
				// During startup we run without threads, do nothing
				return;
			}
			lock.lock ();
			owns_lock = true;
		}

		~StartupAwareLock ()
		{
			if (owns_lock) {
				lock.unlock ();
			}
		}

		StartupAwareLock (StartupAwareLock const&) = delete;
		StartupAwareLock (StartupAwareLock const&&) = delete;

		StartupAwareLock& operator= (StartupAwareLock const&) = delete;

	private:
		std::mutex& lock;
		bool owns_lock = false;
	};
}

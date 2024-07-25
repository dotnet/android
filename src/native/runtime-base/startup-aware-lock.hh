#ifndef __STARTUP_AWARE_LOCK_HH
#define __STARTUP_AWARE_LOCK_HH

#include "cppcompat.hh"
#include "monodroid-state.hh"

namespace xamarin::android::internal
{
	class StartupAwareLock final
	{
	public:
		explicit StartupAwareLock (xamarin::android::mutex &m)
			: lock (m)
		{
			if (MonodroidState::is_startup_in_progress ()) {
				// During startup we run without threads, do nothing
				return;
			}

			lock.lock ();
		}

		~StartupAwareLock ()
		{
			if (MonodroidState::is_startup_in_progress ()) {
				return;
			}

			lock.unlock ();
		}

		StartupAwareLock (StartupAwareLock const&) = delete;
		StartupAwareLock (StartupAwareLock const&&) = delete;

		StartupAwareLock& operator= (StartupAwareLock const&) = delete;

	private:
		xamarin::android::mutex& lock;
	};
}
#endif

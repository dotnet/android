#ifndef __STARTUP_AWARE_LOCK_HH
#define __STARTUP_AWARE_LOCK_HH

#include <runtime-base/mutex.hh>
#include "monodroid-state.hh"

namespace xamarin::android::internal
{
	class StartupAwareLock final
	{
	public:
		explicit StartupAwareLock (xamarin::android::Mutex &m)
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
		xamarin::android::Mutex& lock;
		bool owns_lock = false;
	};
}
#endif

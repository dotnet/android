#pragma once

namespace xamarin::android
{
	class MonodroidState
	{
	public:
		static auto is_startup_in_progress () noexcept -> bool
		{
			return __atomic_load_n (&startup_in_progress, __ATOMIC_ACQUIRE);
		}

		static void mark_startup_done () noexcept
		{
			__atomic_store_n (&startup_in_progress, false, __ATOMIC_RELEASE);
		}

	private:
		inline static bool startup_in_progress = true;
	};
}

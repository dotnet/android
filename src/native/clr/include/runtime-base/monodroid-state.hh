#pragma once

namespace xamarin::android
{
	class MonodroidState
	{
	public:
		static auto is_startup_in_progress () noexcept -> bool
		{
			return startup_in_progress;
		}

		static void mark_startup_done () noexcept
		{
			startup_in_progress = false;
		}

	private:
		inline static bool startup_in_progress = true;
	};
}

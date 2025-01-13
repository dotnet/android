#pragma once

namespace xamarin::android::internal
{
	class MonodroidState
	{
	public:
		static bool is_startup_in_progress () noexcept
		{
			return startup_in_progress;
		}

		static void mark_startup_done ()
		{
			startup_in_progress = false;
		}

	private:
		inline static bool startup_in_progress = true;
	};
}

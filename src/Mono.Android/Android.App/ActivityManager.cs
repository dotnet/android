using System;

using Android.Content;

namespace Android.App {

	public partial class ActivityManager {

		public static ActivityManager FromContext (Context context)
		{
			return context.GetSystemService (Context.ActivityService) as ActivityManager;
		}

#if ANDROID_11
		public void MoveTaskToFront (int taskId, int flags)
		{
			MoveTaskToFront (taskId, (MoveTaskFlags) flags);
		}
#endif
	}
}



using System;

using Android.Content;

namespace Android.App {

	public partial class ActivityManager {

		public static ActivityManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.ActivityService) as ActivityManager;
		}

		public void MoveTaskToFront (int taskId, int flags)
		{
			MoveTaskToFront (taskId, (MoveTaskFlags) flags);
		}
	}
}



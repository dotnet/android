using System;
using Android.Runtime;

namespace Android.Graphics.Drawables {

	public partial class RotateDrawable {

		public void ScheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what, long when)
		{
			ScheduleDrawable (who, Java.Lang.Thread.RunnableImplementor.GetOrCreate (who, what), when);
		}

		public void UnscheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Find (who, what);
			if (runnable == null)
				return;
			UnscheduleDrawable (who, runnable);
		}
	}
}


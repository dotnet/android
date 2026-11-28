using System;
using Android.Runtime;

namespace Android.Graphics.Drawables {

	public partial class Drawable {

		public void ScheduleSelf (Action what, long when)
		{
			ScheduleSelf (Java.Lang.Thread.RunnableImplementor.GetOrCreate (this, what), when);
		}

		public void UnscheduleSelf (Action what)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Find (this, what);
			if (runnable != null)
				UnscheduleSelf (runnable);
			// Also cancel what the hosting View scheduled for this drawable via View.ScheduleDrawable (who, Action, long).
			if (Callback is Android.Views.View view && Java.Lang.Thread.RunnableImplementor.FindScheduled (view, this, what) is { } scheduled)
				UnscheduleSelf (scheduled);
		}
	}
}


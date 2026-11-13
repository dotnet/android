using System;
using Android.Runtime;

namespace Android.Graphics.Drawables {

	public partial class Drawable {

		public void ScheduleSelf (Action what, long when)
		{
			ScheduleSelf (Java.Lang.Thread.RunnableImplementor.GetOrCreate (what), when);
		}

		public void UnscheduleSelf (Action what)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Find (what);
			if (runnable == null)
				return;
			UnscheduleSelf (runnable);
		}
	}
}

